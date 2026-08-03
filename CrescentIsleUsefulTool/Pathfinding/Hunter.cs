using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules;
using CrescentIsleUsefulTool.Modules.Automator;
using CrescentIsleUsefulTool.Modules.MagicPot;
using CrescentIsleUsefulTool.Modules.Pathfinder;
using CrescentIsleUsefulTool.Modules.StateManager;
using CrescentIsleUsefulTool.Modules.Teleporter;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Dalamud.Bindings.ImGui;
using Ocelot;
using Ocelot.Ui;
using Ocelot.Chain;
using Ocelot.IPC;
using Ocelot.Modules;
using TextCopy;

namespace CrescentIsleUsefulTool.Pathfinding;

public abstract class Hunter
{
    protected const float DISTANCE_TO_NODE_TO_USE = 2f;

    private static readonly TimeSpan PathRequestTimeout = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan StationaryStallTimeout = TimeSpan.FromSeconds(12);

    private static readonly TimeSpan DestinationProgressTimeout = TimeSpan.FromSeconds(35);

    protected StateManagerModule states;

    protected VNavmesh vnav;

    protected readonly Module ownerModule;

    protected PathfinderConfig config;

    protected bool running;

    protected IPathfinder? pathfinder;

    protected List<PathfinderStep> Steps = [];

    protected int stepIndex = 0;

    protected float distance = 0f;

    protected Stopwatch stopwatch = new();

    protected string runtimeStatus = "停止中";

    private Task<List<Vector3>>? reachablePathTask;

    private int reachablePathStepIndex = -1;

    private Vector3 reachablePathDestination;

    private Vector3 reachablePathEndpoint;

    private DateTime reachablePathRequestedAtUtc = DateTime.MinValue;

    private int reachablePathFollowFailures;

    private List<Vector3> activeMovementPath = [];

    private int activeMovementPathStepIndex = -1;

    private string unsafeEnemyDescription = "上限を超える敵";

    private int movementProgressStepIndex = -1;

    private Vector3 lastMovementPosition;

    private float lastMovementDistance = float.MaxValue;

    private DateTime lastMovementProgressAtUtc = DateTime.MinValue;

    private DateTime lastDestinationProgressAtUtc = DateTime.MinValue;

    private bool currentStepUnreachable;

    private StepFailureReason currentStepFailureReason;

    private bool movementPathStarted;

    private bool skipNextAethernetTeleport;

    private bool rebuildingAfterUnreachableRoute;

    private readonly Dictionary<string, int> routeRecoveryAttempts = [];

    private bool HasPausedProgress => !running && Steps.Count > 0 && stepIndex < Steps.Count;

    public bool IsRunning => running;

    public string RuntimeStatus => runtimeStatus;

    protected PathfinderStep CurrentStep
    {
        get => Steps[stepIndex];
    }

    protected string JSON = "";

    protected ChainQueue StepProcessor
    {
        get => ChainManager.Get(GetType().FullName ?? "Hunter");
    }

    protected Dictionary<PathfinderStepType, Func<bool>> Handlers;

    protected Hunter(Module module)
    {
        ownerModule = module;
        states = module.GetModule<StateManagerModule>();
        vnav = module.GetIPCSubscriber<VNavmesh>();
        config = module.PluginConfig.PathfinderConfig;

        Handlers = new Dictionary<PathfinderStepType, Func<bool>>
        {
            { PathfinderStepType.WalkToNode, WalkToNodeHandler },
            { PathfinderStepType.ReturnToBaseCamp, ReturnToBaseCampHandler },
            { PathfinderStepType.WalkToAethernet, WalkToAethernetHandler },
            { PathfinderStepType.TeleportToAethernet, TeleportToAethernetHandler },
            { PathfinderStepType.RideTurbulence, RideTurbulenceHandler },
        };
    }

    protected abstract IEnumerable<IGameObject> GetValidObjects();

    protected abstract Vector3 GetDestinationForCurrentStep();

    protected float GetDetectionRange()
    {
        return config.DetectionRange;
    }

    protected StepFailureReason CurrentStepFailureReason => currentStepFailureReason;

    protected virtual float GetInactiveNodeConfirmationRange()
    {
        return GetDetectionRange();
    }

    protected abstract IPathfinder CreatePathfinder();

    protected abstract Func<Chain> GetInteractionChain(IGameObject obj);

    protected abstract List<uint> GetValidNodes(int max);

    protected virtual bool IsPathfinderDataReady()
    {
        return true;
    }

    protected virtual bool HasAvailablePathfinderNodes()
    {
        return true;
    }

    protected virtual bool CanStartHunt => true;

    /// <summary>
    /// Rebuild the remaining route when a paused hunt is started again.
    /// The treasure hunter uses this to return to camp first while retaining
    /// the coordinates that were already completed.
    /// </summary>
    protected virtual bool RebuildRouteOnResume => false;

    /// <summary>
    /// Allows an explicit route-start reset to use Demi-Déjion even inside
    /// the base-camp safety radius. This remains disabled for ordinary hunts.
    /// </summary>
    protected virtual bool AlwaysUseDemiReturnAtRouteStart => false;

    /// <summary>
    /// Forces Magi Treasuresight during the route-start return sequence.
    /// </summary>
    protected virtual bool UpdateTreasureCountAtRouteStart => false;

    protected virtual void OnHuntStarted(bool isResuming)
    {
    }

    protected virtual bool ShouldSkipStep(PathfinderStep step)
    {
        return false;
    }

    protected virtual void OnStepCompleted(PathfinderStep step)
    {
    }

    protected virtual void OnStepUnreachable(PathfinderStep step)
    {
    }

    protected enum StepFailureReason
    {
        None,
        Unreachable,
        Stalled,
        UnsafeEnemy,
    }

    /// <summary>
    /// Opts a hunter into rebuilding the remaining route only after movement
    /// validation has declared the current segment unreachable.
    /// </summary>
    protected virtual bool RebuildRouteAfterUnreachable => false;

    protected virtual string GetRouteRecoveryKey(PathfinderStep step)
    {
        return $"{step.Type}:{step.NodeId}:{step.Aethernet}:{step.Position}";
    }

    protected virtual void OnProgressReset()
    {
    }

    public void Update()
    {
        if (!running)
        {
            return;
        }

        if (!VnavmeshIpc.IsOperational(vnav, out var navigationWaitingReason))
        {
            runtimeStatus = navigationWaitingReason;
            return;
        }

        if (!ownerModule.TryGetIPCSubscriber<Lifestream>(out var lifestream) || lifestream == null)
        {
            runtimeStatus = "Lifestreamプラグインの起動を待っています。";
            return;
        }

        if (!LifestreamIpc.IsOperational(lifestream, out var lifestreamWaitingReason))
        {
            runtimeStatus = lifestreamWaitingReason;
            return;
        }

        if (HasQueueWork(Plugin.Chain))
        {
            runtimeStatus = "ほかの移動処理が完了するのを待っています。";
            return;
        }

        if (pathfinder == null && Steps.Count <= 0)
        {
            if (!IsPathfinderDataReady())
            {
                runtimeStatus = "ゲーム内部の座標データを読み込んでいます。";
                return;
            }

            pathfinder = CreatePathfinder();
            if (!HasAvailablePathfinderNodes())
            {
                pathfinder = null;
                runtimeStatus = "青銅・白銀の宝箱座標を取得しています。";
                return;
            }
        }

        runtimeStatus = Steps.Count == 0 ? "巡回経路を作成しています。" : "宝箱を巡回しています。";
        MaintainWatcherChain();
    }

    private void MaintainWatcherChain()
    {
        if (HasQueueWork(Plugin.Chain))
        {
            return;
        }

        if (pathfinder != null && pathfinder.State != PathfinderState.PathfindingDone)
        {
            Plugin.Chain.Submit(() =>
            {
                Task<List<PathfinderStep>> steps = null!;
                var valid = GetValidNodes(config.MaxLevel);

                // Prep pathfinding
                return Chain.Create("Hunter.Pathfinding")
                    .Then(new TaskManagerTask(() => pathfinder?.State == PathfinderState.FileLoaded))
                    .Then(_ => steps = pathfinder.FindPath(Player.Position, valid))
                    .Then(new TaskManagerTask(() => steps!.IsCompleted))
                    .Then(_ =>
                    {
                        Steps = steps!.Result;
                        stepIndex = 0;
                        ResetMovementValidation();
                    })
                    .Then(_ =>
                    {
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Converters =
                            {
                                new PathfinderStepConverter(),
                            },
                        };

                        JSON = JsonSerializer.Serialize(Steps, options);
                    })
                    .Then(_ => pathfinder = null);
            });

            return;
        }

        if (HasQueueWork(StepProcessor))
        {
            return;
        }

        if (stepIndex >= Steps.Count)
        {
            Teardown();
            return;
        }

        StepProcessor.Submit(() =>
            Chain.Create("Hunter.Run")
                .Then(_ =>
                {
                    var step = CurrentStep;
                    if (ShouldSkipStep(step))
                    {
                        OnStepCompleted(step);
                        ResetMovementValidation();
                        stepIndex++;
                        return;
                    }

                    var handler = Handlers[CurrentStep.Type];
                    if (handler())
                    {
                        if (currentStepUnreachable)
                        {
                            if (TryRebuildAfterUnreachable(step))
                            {
                                ResetMovementValidation();
                                return;
                            }

                            OnStepUnreachable(step);
                        }
                        else
                        {
                            OnStepCompleted(step);
                        }

                        ResetMovementValidation();
                        stepIndex++;
                        if (step.Type == PathfinderStepType.ReturnToBaseCamp)
                        {
                            rebuildingAfterUnreachableRoute = false;
                        }
                    }
                })
                .Wait(1000 / 60)
        );
    }

    public void Draw(Module<Plugin, Config> module)
    {
        CrescentTheme.Card($"Hunter_{GetType().Name}", module.T("panel.hunt.title"), () =>
        {
            if (ImGui.Button(running ? I18N.T("generic.label.stop") : I18N.T("generic.label.start")))
            {
                if (running)
                {
                    Pause();
                }
                else
                {
                    TryStartHunt(disableAutomationMode: true, runStartupPreparation: true);
                }
            }

            if (HasPausedProgress)
            {
                ImGui.SameLine();
                if (ImGui.Button("最初から##ResetHunterRoute"))
                {
                    ResetProgress();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("保存した巡回位置を破棄し、次回は新しい経路を作成します。");
                }
            }

            if (running || HasPausedProgress)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("実行状態");
                ImGui.TextWrapped(HasPausedProgress ? $"一時停止中（{stepIndex}/{Steps.Count}から再開できます）" : runtimeStatus);
            }

            if (stopwatch.Elapsed > TimeSpan.Zero)
            {
                ImGui.SameLine();
                if (ImGui.Button(I18N.T("hunter.export.label")))
                {
                    ClipboardService.SetText(JSON);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(I18N.T("hunter.export.tooltip"));
                }

                ImGui.TextDisabled(I18N.T("hunter.elapsed"));
                ImGui.SameLine();
                ImGui.TextColored(CrescentTheme.AccentSoft, $"{stopwatch.Elapsed:mm\\:ss}");
            }


            if ((running || HasPausedProgress) && stepIndex < Steps.Count)
            {
                OcelotUi.LabelledValue(I18N.T("hunter.progress"), $"{stepIndex}/{Steps.Count}");

                if (CurrentStep.Type == PathfinderStepType.WalkToNode)
                {
                    OcelotUi.LabelledValue(module.T("panel.hunt.distance_node"), $"{distance:f2}/{GetDetectionRange():f2}");
                }

                if (CurrentStep.Type == PathfinderStepType.WalkToAethernet)
                {
                    OcelotUi.LabelledValue(I18N.T("hunter.distance_shard"), $"{distance:f2}");
                }
            }
        }, GetRouteDescription());
    }

    public bool StartForAutomation(bool runStartupPreparation)
    {
        return TryStartHunt(disableAutomationMode: false, runStartupPreparation);
    }

    private bool TryStartHunt(bool disableAutomationMode, bool runStartupPreparation)
    {
        if (running)
        {
            return true;
        }

        if (!CanStartHunt)
        {
            runtimeStatus = "巡回対象の宝箱座標はすべて確認済みです。";
            return false;
        }

        if (ownerModule.TryGetModule<TeleporterModule>(out var teleporter) &&
            teleporter?.teleporter.IsCompletionReturnPending == true)
        {
            runtimeStatus = "FATE・CE完了後の拠点帰還が完了してから開始できます。";
            return false;
        }

        if (ownerModule.TryGetModule<MagicPotModule>(out var magicPot) &&
            magicPot?.IsTreasureSearchActive == true)
        {
            runtimeStatus = "マジックポットの宝箱探索が完了してから開始できます。";
            return false;
        }

        if (disableAutomationMode &&
            ownerModule.TryGetModule<AutomatorModule>(out var automator) &&
            automator?.Config.Enabled == true)
        {
            automator.DisableAutomationMode();
        }

        var isResuming = HasPausedProgress;
        var rebuildRemainingRoute = isResuming && RebuildRouteOnResume;
        if (rebuildRemainingRoute)
        {
            stepIndex = 0;
            Steps.Clear();
            pathfinder = null;
        }

        ResetMovementValidation();
        StepProcessor.Abort();
        running = true;
        if (runStartupPreparation)
        {
            OnHuntStarted(isResuming);
        }

        if (isResuming)
        {
            stopwatch.Start();
            runtimeStatus = rebuildRemainingRoute
                ? "完了済み座標を除外し、残りの固定経路を再構築します。"
                : $"中断地点から再開します（{stepIndex}/{Steps.Count}）。";
        }
        else
        {
            stepIndex = 0;
            Steps.Clear();
            pathfinder = null;
            stopwatch.Restart();
            runtimeStatus = runStartupPreparation ? "開始準備中です。" : "巡回経路を作成しています。";
        }

        return true;
    }

    protected virtual string GetRouteDescription()
    {
        return "現在地から近い順に巡回します。";
    }

    protected virtual void Teardown()
    {
        stopwatch.Stop();
        running = false;
        stepIndex = 0;
        Steps.Clear();
        VnavmeshIpc.TryCancelAllPathfinds(vnav);
        VnavmeshIpc.TryStop(vnav);
        Plugin.Chain.Abort();
        StepProcessor.Abort();
        pathfinder = null;
        ResetMovementValidation();
        skipNextAethernetTeleport = false;
        rebuildingAfterUnreachableRoute = false;
        routeRecoveryAttempts.Clear();
    }

    public void ResetForTerritoryChange()
    {
        ResetProgress();
    }

    public void PauseForConflictingMode(string reason)
    {
        if (!running)
        {
            return;
        }

        Pause();
        runtimeStatus = reason;
        Svc.Log.Warning(reason);
    }

    private void Pause()
    {
        stopwatch.Stop();
        running = false;
        VnavmeshIpc.TryCancelAllPathfinds(vnav);
        VnavmeshIpc.TryStop(vnav);
        Plugin.Chain.Abort();
        StepProcessor.Abort();
        ResetMovementValidation();
        skipNextAethernetTeleport = false;

        // A route that has already been constructed is intentionally retained.
        // If pathfinding had not completed yet, rebuild it on the next start.
        if (Steps.Count == 0)
        {
            pathfinder = null;
        }

        runtimeStatus = Steps.Count > 0 ? "一時停止中" : "停止中";
    }

    private void ResetProgress()
    {
        stopwatch.Reset();
        running = false;
        stepIndex = 0;
        Steps.Clear();
        JSON = "";
        VnavmeshIpc.TryCancelAllPathfinds(vnav);
        VnavmeshIpc.TryStop(vnav);
        Plugin.Chain.Abort();
        StepProcessor.Abort();
        pathfinder = null;
        runtimeStatus = "停止中";
        ResetMovementValidation();
        skipNextAethernetTeleport = false;
        rebuildingAfterUnreachableRoute = false;
        routeRecoveryAttempts.Clear();
        OnProgressReset();
    }


    protected bool WalkToNodeHandler()
    {
        var destination = GetDestinationForCurrentStep();
        var layoutDistance = Player.DistanceTo(destination);
        var obj = layoutDistance <= GetDetectionRange()
            ? GetValidObjects().FirstOrDefault(o => Vector3.Distance(destination, o.Position) <= 5f)
            : null;
        var movementDestination = obj?.Position ?? destination;

        if (TryGetOverLevelEnemyNear(movementDestination, out var nearbyEnemy))
        {
            MarkCurrentStepUnreachable(
                $"{nearbyEnemy}が宝箱座標付近にいるため、この地点は巡回対象から除外します。",
                StepFailureReason.UnsafeEnemy);
            return true;
        }

        if (TryGetUnsafeEnemyOnCurrentRoute(movementDestination, out var unsafeEnemy))
        {
            MarkCurrentStepUnreachable(unsafeEnemy, StepFailureReason.UnsafeEnemy);
            return true;
        }

        distance = Player.DistanceTo(movementDestination);

        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (!isRunning)
        {
            var movementState = StartReachableMovement(movementDestination);
            if (movementState == ReachableMovementState.UnsafeEnemy)
            {
                MarkCurrentStepUnreachable($"{unsafeEnemyDescription}が経路上にいるため、この宝箱座標へは近づきません。", StepFailureReason.UnsafeEnemy);
                return true;
            }
            if (movementState == ReachableMovementState.Unreachable)
            {
                MarkCurrentStepUnreachable("ナビメッシュ上で到達できない宝箱座標をスキップしました。", StepFailureReason.Unreachable);
                return true;
            }
        }

        if (HasMovementStalled(distance))
        {
            MarkCurrentStepUnreachable("移動の進捗がないため、この宝箱座標をスキップしました。", StepFailureReason.Stalled);
            return true;
        }

        if (!Player.Mounted && distance > DISTANCE_TO_NODE_TO_USE)
        {
            StepProcessor.SubmitFront(ChainHelper.MountChain());
        }

        if (obj != null)
        {
            if (distance <= DISTANCE_TO_NODE_TO_USE)
            {
                VnavmeshIpc.TryStop(vnav);
                StepProcessor.SubmitFront(GetInteractionChain(obj));
                return true;
            }

            return false;
        }

        if (layoutDistance <= GetInactiveNodeConfirmationRange())
        {
            // This placement is not active for the current player, or it has
            // already been opened. Continue to the next internal coordinate.
            VnavmeshIpc.TryStop(vnav);
            return true;
        }

        return distance <= DISTANCE_TO_NODE_TO_USE;
    }

    private bool ReturnToBaseCampHandler()
    {
        var inCombat = states.GetState() == State.InCombat;
        var baseCamp = ZoneData.IsInNorthHorn() ? Aethernet.NorthBaseCamp : Aethernet.BaseCamp;
        distance = Player.DistanceTo(baseCamp.GetData().Position);

        // If we are in combat, start running back to the base camp so we can escape combat
        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (inCombat && !isRunning)
        {
            VnavmeshIpc.TryPathfindAndMoveTo(vnav, baseCamp.GetData().Position, false);
            ResetMovementProgress(distance);
            return false;
        }

        if (inCombat && HasMovementStalled(distance))
        {
            VnavmeshIpc.TryCancelAllPathfinds(vnav);
            VnavmeshIpc.TryStop(vnav);
            ResetMovementProgress(distance);
            runtimeStatus = "戦闘解除中の移動停止を検知したため、ベースキャンプへの経路を再作成します。";
            Svc.Log.Warning(runtimeStatus);
            return false;
        }

        if (!inCombat && isRunning)
        {
            VnavmeshIpc.TryStop(vnav);
        }

        if (inCombat)
        {
            return false;
        }

        StepProcessor.SubmitFront(ChainHelper.ReturnChain(new ReturnChainConfig
        {
            ApproachAetheryte = true,
            ForceReturn = ZoneData.IsInNorthHorn(),
            AlwaysUseDemiReturn = AlwaysUseDemiReturnAtRouteStart && !rebuildingAfterUnreachableRoute,
            WaitForStationaryDemiReturn = AlwaysUseDemiReturnAtRouteStart,
            ApplyBuffs = true,
            UpdateTreasureCount = UpdateTreasureCountAtRouteStart,
        }));

        return true;
    }

    private bool WalkToAethernetHandler()
    {
        var destination = CurrentStep.Aethernet.GetData().Position;

        if (TryGetUnsafeEnemyOnCurrentRoute(destination, out var unsafeEnemy))
        {
            MarkCurrentStepUnreachable(unsafeEnemy, StepFailureReason.UnsafeEnemy);
            return true;
        }

        distance = Player.DistanceTo(destination);
        if (distance <= 4f)
        {
            skipNextAethernetTeleport = false;
            VnavmeshIpc.TryStop(vnav);
            return true;
        }

        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (!isRunning)
        {
            var movementState = StartReachableMovement(destination);
            if (movementState == ReachableMovementState.UnsafeEnemy)
            {
                MarkCurrentStepUnreachable($"{unsafeEnemyDescription}が経路上にいるため、この魔導通路へは近づきません。", StepFailureReason.UnsafeEnemy);
                return true;
            }
            if (movementState == ReachableMovementState.Unreachable)
            {
                MarkCurrentStepUnreachable("魔導通路まで到達できないため、この移動区間を中止しました。", StepFailureReason.Unreachable);
                return true;
            }
        }

        if (HasMovementStalled(distance))
        {
            MarkCurrentStepUnreachable("魔導通路への移動が停止したため、この移動区間を中止しました。", StepFailureReason.Stalled);
            return true;
        }

        if (!Player.Mounted)
        {
            StepProcessor.SubmitFront(ChainHelper.MountChain());
        }

        return false;
    }

    private bool TeleportToAethernetHandler()
    {
        distance = 0;
        if (skipNextAethernetTeleport)
        {
            skipNextAethernetTeleport = false;
            MarkCurrentStepUnreachable("到達できなかった魔導通路からの転送をスキップしました。", StepFailureReason.Unreachable);
            return true;
        }

        StepProcessor.SubmitFront(ChainHelper.TeleportChain(CurrentStep.Aethernet));
        return true;
    }

    private bool RideTurbulenceHandler()
    {
        var trigger = CurrentStep.Position;
        var arrival = CurrentStep.ArrivalPosition;

        if (TryGetUnsafeEnemyOnCurrentRoute(trigger, out var unsafeEnemy))
        {
            MarkCurrentStepUnreachable(unsafeEnemy, StepFailureReason.UnsafeEnemy);
            return true;
        }
        distance = Player.DistanceTo(trigger);

        if (HasReachedTurbulenceArrival(arrival))
        {
            VnavmeshIpc.TryStop(vnav);
            return true;
        }

        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (!isRunning)
        {
            var movementState = StartReachableMovement(trigger);
            if (movementState == ReachableMovementState.UnsafeEnemy)
            {
                MarkCurrentStepUnreachable($"{unsafeEnemyDescription}が経路上にいるため、この乱気流へは近づきません。", StepFailureReason.UnsafeEnemy);
                return true;
            }
            if (movementState == ReachableMovementState.Unreachable)
            {
                MarkCurrentStepUnreachable("乱気流まで到達できないため、この経路をスキップしました。", StepFailureReason.Unreachable);
                return true;
            }
        }

        if (HasMovementStalled(distance))
        {
            MarkCurrentStepUnreachable("乱気流への移動が停止したため、この経路をスキップしました。", StepFailureReason.Stalled);
            return true;
        }

        if (!Player.Mounted && distance > DISTANCE_TO_NODE_TO_USE)
        {
            StepProcessor.SubmitFront(ChainHelper.MountChain());
        }

        return false;
    }

    private static bool HasReachedTurbulenceArrival(Vector3 arrival)
    {
        var horizontalDistance = Vector2.Distance(
            new Vector2(Player.Position.X, Player.Position.Z),
            new Vector2(arrival.X, arrival.Z));
        return MathF.Abs(Player.Position.Y - arrival.Y) <= 15f && horizontalDistance <= 35f;
    }

    private ReachableMovementState StartReachableMovement(Vector3 destination)
    {
        if (reachablePathStepIndex != stepIndex || Vector3.Distance(reachablePathDestination, destination) > 2f)
        {
            reachablePathTask = null;
            reachablePathStepIndex = stepIndex;
            reachablePathDestination = destination;
            reachablePathEndpoint = destination;
            reachablePathFollowFailures = 0;
            if (VnavmeshIpc.TryFindPointOnFloor(vnav, destination, false, 4f, out var floorPoint) && floorPoint.HasValue)
            {
                reachablePathEndpoint = floorPoint.Value;
            }
            reachablePathRequestedAtUtc = DateTime.UtcNow;
        }

        if (reachablePathTask == null)
        {
            if (!VnavmeshIpc.TryPathfind(vnav, Player.Position, reachablePathEndpoint, false, out reachablePathTask) || reachablePathTask == null)
            {
                return DateTime.UtcNow - reachablePathRequestedAtUtc > PathRequestTimeout
                    ? ReachableMovementState.Unreachable
                    : ReachableMovementState.Pending;
            }

            reachablePathRequestedAtUtc = DateTime.UtcNow;
            return ReachableMovementState.Pending;
        }

        if (!reachablePathTask.IsCompleted)
        {
            return DateTime.UtcNow - reachablePathRequestedAtUtc > PathRequestTimeout
                ? ReachableMovementState.Unreachable
                : ReachableMovementState.Pending;
        }

        if (reachablePathTask.IsCanceled || reachablePathTask.IsFaulted)
        {
            return ReachableMovementState.Unreachable;
        }

        List<Vector3> path;
        try
        {
            path = reachablePathTask.Result;
        }
        catch
        {
            return ReachableMovementState.Unreachable;
        }

        reachablePathTask = null;
        if (path.Count == 0)
        {
            return ReachableMovementState.Unreachable;
        }

        activeMovementPath = path;
        activeMovementPathStepIndex = stepIndex;
        if (TryGetUnsafeEnemyOnCurrentRoute(destination, out _))
        {
            return ReachableMovementState.UnsafeEnemy;
        }

        if (!VnavmeshIpc.TryFollowPath(vnav, path, false))
        {
            reachablePathFollowFailures++;
            return reachablePathFollowFailures >= 3
                ? ReachableMovementState.Unreachable
                : ReachableMovementState.Pending;
        }

        reachablePathFollowFailures = 0;
        movementPathStarted = true;
        if (movementProgressStepIndex != stepIndex)
        {
            ResetMovementProgress();
        }
        return ReachableMovementState.Started;
    }

    private bool TryGetUnsafeEnemyOnCurrentRoute(Vector3 destination, out string message)
    {
        message = "";
        if (activeMovementPathStepIndex != stepIndex || activeMovementPath.Count == 0)
        {
            return false;
        }

        var position = Player.Position;
        var closestPathIndex = 0;
        var closestPathDistance = float.MaxValue;
        for (var index = 0; index < activeMovementPath.Count; index++)
        {
            var candidateDistance = Vector3.DistanceSquared(position, activeMovementPath[index]);
            if (candidateDistance < closestPathDistance)
            {
                closestPathDistance = candidateDistance;
                closestPathIndex = index;
            }
        }

        var futurePath = activeMovementPath.Skip(closestPathIndex).Append(destination).ToArray();
        foreach (var enemy in TargetHelper.Enemies.Where(enemy =>
                     enemy.IsValid() &&
                     enemy.IsTargetable &&
                     !enemy.IsDead &&
                     enemy.Level > config.MaxLevel))
        {
            var currentDistance = Vector3.Distance(position, enemy.Position);
            var futureDistance = futurePath.Min(point => Vector3.Distance(point, enemy.Position));
            const float avoidanceRadius = 25f;
            if (futureDistance >= avoidanceRadius || futureDistance >= currentDistance - 1f)
            {
                continue;
            }

            var enemyName = enemy.Name.TextValue;
            if (string.IsNullOrWhiteSpace(enemyName))
            {
                enemyName = "敵";
            }

            unsafeEnemyDescription = $"Lv.{enemy.Level} {enemyName}";
            message = $"{unsafeEnemyDescription}を経路上に検知しました。最大エリアレベル Lv.{config.MaxLevel} を超えるため近づきません。";
            return true;
        }

        return false;
    }

    private bool TryGetOverLevelEnemyNear(Vector3 destination, out string description)
    {
        const float avoidanceRadius = 25f;
        var enemy = TargetHelper.Enemies
            .Where(candidate => candidate.IsValid() && candidate.IsTargetable && !candidate.IsDead)
            .Where(candidate => candidate.Level > config.MaxLevel)
            .Where(candidate => Vector3.Distance(candidate.Position, destination) < avoidanceRadius)
            .OrderBy(candidate => Vector3.Distance(candidate.Position, destination))
            .FirstOrDefault();
        if (enemy == null)
        {
            description = "";
            return false;
        }

        var name = enemy.Name.TextValue;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "敵";
        }

        description = $"Lv.{enemy.Level} {name}（設定上限 Lv.{config.MaxLevel}）";
        unsafeEnemyDescription = $"Lv.{enemy.Level} {name}";
        return true;
    }

    private bool HasMovementStalled(float currentDistance)
    {
        if (movementProgressStepIndex != stepIndex)
        {
            ResetMovementProgress(currentDistance);
            return false;
        }

        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (!isRunning && !Player.IsMoving)
        {
            return movementPathStarted && DateTime.UtcNow - lastMovementProgressAtUtc > StationaryStallTimeout;
        }

        var moved = Vector3.Distance(Player.Position, lastMovementPosition);
        if (moved >= 1.5f)
        {
            lastMovementPosition = Player.Position;
            lastMovementProgressAtUtc = DateTime.UtcNow;
        }

        if (currentDistance <= lastMovementDistance - 1f)
        {
            lastMovementDistance = currentDistance;
            lastDestinationProgressAtUtc = DateTime.UtcNow;
        }

        return DateTime.UtcNow - lastMovementProgressAtUtc > StationaryStallTimeout ||
               DateTime.UtcNow - lastDestinationProgressAtUtc > DestinationProgressTimeout;
    }

    private void ResetMovementProgress(float currentDistance = float.MaxValue)
    {
        movementProgressStepIndex = stepIndex;
        lastMovementPosition = Player.Position;
        lastMovementDistance = currentDistance;
        lastMovementProgressAtUtc = DateTime.UtcNow;
        lastDestinationProgressAtUtc = DateTime.UtcNow;
    }

    private void MarkCurrentStepUnreachable(string message, StepFailureReason reason = StepFailureReason.Unreachable)
    {
        currentStepUnreachable = true;
        currentStepFailureReason = reason;
        if (CurrentStep.Type == PathfinderStepType.WalkToAethernet)
        {
            // A travel segment stores the destination aethernet on its next
            // step, so enum equality cannot identify a failed source walk.
            skipNextAethernetTeleport = true;
        }
        runtimeStatus = message;
        VnavmeshIpc.TryStop(vnav);
        Svc.Log.Warning(message);
    }

    private bool TryRebuildAfterUnreachable(PathfinderStep failedStep)
    {
        if (!RebuildRouteAfterUnreachable)
        {
            return false;
        }

        var key = GetRouteRecoveryKey(failedStep);
        var attempt = routeRecoveryAttempts.GetValueOrDefault(key) + 1;
        routeRecoveryAttempts[key] = attempt;

        // First failure rebuilds from the current set of remaining locations.
        // A repeated failure of the same destination is recorded as unreachable
        // before rebuilding again so the hunt cannot loop forever.
        if (currentStepFailureReason == StepFailureReason.UnsafeEnemy || attempt >= 2)
        {
            OnStepUnreachable(failedStep);
        }

        VnavmeshIpc.TryCancelAllPathfinds(vnav);
        VnavmeshIpc.TryStop(vnav);
        StepProcessor.Clear();
        Steps.Clear();
        stepIndex = 0;
        pathfinder = null;
        skipNextAethernetTeleport = false;
        rebuildingAfterUnreachableRoute = true;
        runtimeStatus = attempt == 1
            ? "進行不能を検知したため、残り座標の経路を再作成しています。"
            : "同じ区間が再び進行不能になったため、対象を除外して経路を再作成しています。";
        Svc.Log.Warning($"Treasure route recovery requested after unreachable segment (attempt {attempt}, key {key}).");
        return true;
    }

    private void ResetMovementValidation()
    {
        reachablePathTask = null;
        reachablePathStepIndex = -1;
        reachablePathDestination = default;
        reachablePathEndpoint = default;
        reachablePathRequestedAtUtc = DateTime.MinValue;
        reachablePathFollowFailures = 0;
        activeMovementPath.Clear();
        activeMovementPathStepIndex = -1;
        unsafeEnemyDescription = "上限を超える敵";
        movementProgressStepIndex = -1;
        lastMovementPosition = default;
        lastMovementDistance = float.MaxValue;
        lastMovementProgressAtUtc = DateTime.MinValue;
        lastDestinationProgressAtUtc = DateTime.MinValue;
        currentStepUnreachable = false;
        currentStepFailureReason = StepFailureReason.None;
        movementPathStarted = false;
    }

    private static bool HasQueueWork(ChainQueue queue)
    {
        return queue.IsRunning || queue.QueueCount > 0;
    }

    private enum ReachableMovementState
    {
        Pending,
        Started,
        Unreachable,
        UnsafeEnemy,
    }
}
