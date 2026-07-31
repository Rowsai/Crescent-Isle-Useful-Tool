using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using BOCCHI.Chains;
using BOCCHI.Data;
using BOCCHI.Enums;
using BOCCHI.Ipc;
using BOCCHI.Modules;
using BOCCHI.Modules.Automator;
using BOCCHI.Modules.Pathfinder;
using BOCCHI.Modules.StateManager;
using BOCCHI.Ui;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using Dalamud.Bindings.ImGui;
using Ocelot;
using Ocelot.Ui;
using Ocelot.Chain;
using Ocelot.IPC;
using Ocelot.Modules;
using TextCopy;

namespace BOCCHI.Pathfinding;

public abstract class Hunter
{
    protected const float DISTANCE_TO_NODE_TO_USE = 2f;

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
        };
    }

    protected abstract IEnumerable<IGameObject> GetValidObjects();

    protected abstract Vector3 GetDestinationForCurrentStep();

    protected float GetDetectionRange()
    {
        return config.DetectionRange;
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
                    .Then(_ => Steps = steps!.Result)
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
                    var handler = Handlers[CurrentStep.Type];
                    if (handler())
                    {
                        stepIndex++;
                    }
                })
                .Wait(1000 / 60)
        );

        var obj = GetValidObjects().FirstOrDefault(o => Vector3.Distance(Player.Position, o.Position) <= 5f);
        if (obj != null)
        {
            StepProcessor.Submit(GetInteractionChain(obj));
        }
    }

    public void Draw(Module<Plugin, Config> module)
    {
        CrescentTheme.Card($"Hunter_{GetType().Name}", module.T("panel.hunt.title"), () =>
        {
            if (ImGui.Button(running ? I18N.T("generic.label.stop") : I18N.T("generic.label.start")))
            {
                running = !running;
                if (running == false)
                {
                    stopwatch.Stop();
                    running = false;
                    stepIndex = 0;
                    Steps.Clear();
                    VnavmeshIpc.TryStop(vnav);
                    Plugin.Chain.Abort();
                    StepProcessor.Abort();
                    pathfinder = null;
                }
                else
                {
                    if (ownerModule.TryGetModule<AutomatorModule>(out var automator) && automator?.Config.Enabled == true)
                    {
                        automator.DisableIllegalMode();
                    }

                    stopwatch.Restart();
                    runtimeStatus = "開始準備中です。";
                }
            }

            if (running)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("実行状態");
                ImGui.TextWrapped(runtimeStatus);
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


            if (running && stepIndex < Steps.Count)
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
        VnavmeshIpc.TryStop(vnav);
        Plugin.Chain.Abort();
        StepProcessor.Abort();
        pathfinder = null;
    }


    protected bool WalkToNodeHandler()
    {
        var destination = GetDestinationForCurrentStep();
        var layoutDistance = Player.DistanceTo(destination);
        var obj = layoutDistance <= GetDetectionRange()
            ? GetValidObjects().FirstOrDefault(o => Vector3.Distance(destination, o.Position) <= 5f)
            : null;
        var movementDestination = obj?.Position ?? destination;

        distance = Player.DistanceTo(movementDestination);

        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (!isRunning)
        {
            VnavmeshIpc.TryPathfindAndMoveTo(vnav, movementDestination, false);
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

        if (layoutDistance <= GetDetectionRange())
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
        distance = 0;
        var inCombat = states.GetState() == State.InCombat;
        var baseCamp = ZoneData.IsInNorthHorn() ? Aethernet.NorthBaseCamp : Aethernet.BaseCamp;

        // If we are in combat, start running back to the base camp so we can escape combat
        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (inCombat && !isRunning)
        {
            VnavmeshIpc.TryPathfindAndMoveTo(vnav, baseCamp.GetData().Position, false);
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
            ApplyBuffs = false,
        }));

        return true;
    }

    private bool WalkToAethernetHandler()
    {
        var destination = CurrentStep.Aethernet.GetData().Position;

        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (!isRunning)
        {
            VnavmeshIpc.TryPathfindAndMoveTo(vnav, destination, false);
        }

        if (!Player.Mounted)
        {
            StepProcessor.SubmitFront(ChainHelper.MountChain());
        }

        distance = Player.DistanceTo(destination);
        return distance <= 4f;
    }

    private bool TeleportToAethernetHandler()
    {
        distance = 0;
        StepProcessor.SubmitFront(ChainHelper.TeleportChain(CurrentStep.Aethernet));
        return true;
    }

    private static bool HasQueueWork(ChainQueue queue)
    {
        return queue.IsRunning || queue.QueueCount > 0;
    }
}
