using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.Buff;
using CrescentIsleUsefulTool.Modules.CriticalEncounters;
using CrescentIsleUsefulTool.Modules.Fates;
using CrescentIsleUsefulTool.Modules.MagicPot;
using CrescentIsleUsefulTool.Modules.StateManager;
using CrescentIsleUsefulTool.Modules.Teleporter;
using CrescentIsleUsefulTool.Modules.Treasure;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Chain;
using Ocelot.IPC;
using TeleporterController = CrescentIsleUsefulTool.Modules.Teleporter.Teleporter;

namespace CrescentIsleUsefulTool.Modules.Automator;

public class Automator
{
    private static bool IsChainActive
    {
        // ChainManager.Queues contains idle queues for a short time. Counting
        // dictionary entries made this true almost permanently and prevented
        // the selected FATE/CE chain from ever being submitted.
        get => Plugin.Chain.IsRunning || Plugin.Chain.QueueCount > 0;
    }

    public Activity? Activity { get; private set; } = null;

    private double idleTime = 0;

    private bool waitingForMagicPot;

    private Activity? watchedActivity;

    private ActivityState watchedActivityState;

    private Vector3 lastActivityPosition;

    private float lastActivityDistance = float.MaxValue;

    private DateTime lastActivityProgressUtc = DateTime.MinValue;

    private int activityRecoveryAttempts;

    private readonly Dictionary<string, DateTime> activityCooldowns = [];

    public bool IsWaitingForMagicPot => waitingForMagicPot;

    public string RuntimeStatus { get; private set; } = "停止中";

    public void SetRuntimeStatus(string status)
    {
        RuntimeStatus = status;
    }

    public void PostUpdate(AutomatorModule module, IFramework framework)
    {
        if (module.TryGetModule<MagicPotModule>(out var magicPot) && magicPot?.IsTreasureSearchActive == true)
        {
            module.PauseAutomatedTreasureHunt(
                "マジックポット宝箱探索を優先するため、通常宝箱巡回を一時停止しました。");
            SetRuntimeStatus("マジックポットの宝箱探索を優先しています。");
            return;
        }

        if (!module.TryGetIPCSubscriber<VNavmesh>(out var vnav) || vnav == null)
        {
            SetRuntimeStatus("vnavmeshプラグインの起動を待っています。");
            return;
        }

        if (!VnavmeshIpc.IsOperational(vnav, out var navigationWaitingReason))
        {
            SetRuntimeStatus(navigationWaitingReason);
            return;
        }

        if (!module.TryGetIPCSubscriber<Lifestream>(out var lifestream) || lifestream == null)
        {
            SetRuntimeStatus("Lifestreamプラグインの起動を待っています。");
            return;
        }

        if (!LifestreamIpc.IsOperational(lifestream, out var lifestreamWaitingReason))
        {
            SetRuntimeStatus(lifestreamWaitingReason);
            return;
        }

        SetRuntimeStatus(Activity == null
            ? GetMonitoringStatus(module)
            : $"{Activity.GetName()}：{Activity.state.ToLabel()}");

        var states = module.GetModule<StateManagerModule>();
        var teleporter = module.GetModule<TeleporterModule>().teleporter;
        if (Activity != null)
        {
            module.PauseAutomatedTreasureHunt(
                "FATE・CEを優先するため、通常宝箱巡回を一時停止しました。");
        }

        if (Activity != null && Activity.state == ActivityState.Done)
        {
            CompleteActivity(Activity, module, vnav);
            return;
        }

        if (Activity != null && !Activity.IsValid())
        {
            // Tracker removal and the state transition to Idle are not atomic.
            // Waiting for both used to lose the completion return when the
            // event disappeared one frame before StateManager reached Idle.
            // An event can end while we are still travelling and before the
            // participation state is observed. Treat disappearance as a
            // completion in both cases so the character never stops there.
            CompleteActivity(Activity, module, vnav);
            return;
        }

        // Completion return is a mandatory, indivisible operation. Do not let
        // a newly spawned activity cancel it between Demi-Déjion and arrival at
        // the base aetheryte.
        if (teleporter.IsCompletionReturnPending)
        {
            SetRuntimeStatus(teleporter.CompletionReturnStatus);
            return;
        }

        // The master switch grants permission only. Until the user selects CE,
        // FATE or treasure hunting, CIUT must not move, buff or return by itself.
        if (Activity == null && !module.HasSelectedOperation)
        {
            idleTime = 0;
            SetRuntimeStatus("実行機能が未選択です。CE移動、FATE移動、トレジャーハントから選択してください。");
            return;
        }

        if (Activity != null && UpdateActivityProgressWatchdog(module, lifestream, vnav, teleporter))
        {
            return;
        }

        var forceMagicPotSelection = false;
        if (ShouldWaitForMagicPot(module, magicPot, states))
        {
            if (!waitingForMagicPot)
            {
                module.PauseAutomatedTreasureHunt(
                    "マジックポットFATE待機を優先するため、通常宝箱巡回を一時停止しました。");
                waitingForMagicPot = true;
                Activity = null;
                idleTime = 0;
                ResetActivityWatchdog();
                Plugin.Chain.Abort();
                VnavmeshIpc.TryStop(vnav);
                if (module.Config.ShouldToggleAiProvider)
                {
                    module.Config.AiProvider.Off();
                }
            }

            var remaining = GetMagicPotRemaining(magicPot);
            SetRuntimeStatus($"マジックポットFATE待機中（予想まで {FormatRemaining(remaining)}）");
            return;
        }

        if (waitingForMagicPot)
        {
            forceMagicPotSelection = magicPot?.IsNorthPotActive == true;
            waitingForMagicPot = false;
        }

        if (Activity == null)
        {
            if (states.GetState() == State.InCriticalEncounter)
            {
                var critical = module.GetModule<CriticalEncountersModule>();
                var encounters = critical.CriticalEncounters.Values
                    .Where(ev => ev.State != DynamicEventState.Inactive)
                    .ToList();
                if (encounters.Count == 0)
                {
                    SetRuntimeStatus("CEの終了状態を確認しています。");
                    return;
                }

                var encounter = encounters[^1];
                var data = EventData.GetCriticalEncounter(encounter.DynamicEventId);
                Activity = new CriticalEncounter(data, lifestream, vnav, module, critical);
                module.PauseAutomatedTreasureHunt(
                    "参加中のCEを優先するため、通常宝箱巡回を一時停止しました。");
                module.Debug($"Resuming running activity: {Activity.GetName()}");
                return;
            }

            if (states.GetState() == State.InFate)
            {
                Activity = FindFate(module, lifestream, vnav);
                if (Activity != null)
                {
                    module.PauseAutomatedTreasureHunt(
                        "参加中のFATEを優先するため、通常宝箱巡回を一時停止しました。");
                    module.Debug($"Resuming running activity: {Activity.GetName()}");
                }

                return;
            }
        }

        // Never cancel たんきゅうしん between the support-job change and
        // restoration. The previous selection order could abort this sequence
        // as soon as an event appeared, leaving the player as すっぴん without
        // applying the configured buffs.
        if (Activity == null && IsProtectedBuffSequenceActive())
        {
            SetRuntimeStatus("ナレッジクリスタルへ移動し、たんきゅうしん実行後に元のサポートジョブへ戻します。");
            return;
        }

        // CE/FATE destinations take precedence over an already queued utility
        // chain (for example, returning to an aetheryte).  Selecting them before
        // the chain guard lets automation mode immediately start travelling to the
        // event's target point when it appears.
        if (Activity == null)
        {
            // An imminent-pot wait is a safety override. Otherwise use the
            // user-defined order from the main-window priority tab.
            Activity = forceMagicPotSelection
                ? FindFate(module, lifestream, vnav, FateSelection.MagicPot)
                : FindActivityByPriority(module, lifestream, vnav);
            if (Activity != null)
            {
                idleTime = 0;
                module.PauseAutomatedTreasureHunt(
                    "FATE・CEを検知したため、通常宝箱巡回を一時停止しました。");
                Plugin.Chain.Abort();
                VnavmeshIpc.TryStop(vnav);
                Svc.Log.Info($"Selected priority activity: {Activity.GetName()}");
                SetRuntimeStatus($"{Activity.GetName()}を検知しました。移動を開始します。");
            }
        }

        if (Activity == null && module.TryRunAutomatedTreasureHunt())
        {
            SetRuntimeStatus("宝箱自動モード：地上の青銅・白銀座標を巡回しています。");
            return;
        }

        if (IsChainActive)
        {
            return;
        }

        if (Activity != null)
        {
            var chain = Activity.GetChain(states);
            if (chain == null)
            {
                return;
            }

            Plugin.Chain.Submit(chain);
            SetRuntimeStatus($"{Activity.GetName()}：{Activity.state.ToLabel()}");
            return;
        }

        var baseCamp = (ZoneData.IsInNorthHorn() ? Aethernet.NorthBaseCamp : Aethernet.BaseCamp).GetData();
        var buffs = module.GetModule<BuffModule>();
        if (buffs.ShouldRefreshBuffs() && ZoneData.GetNearbyKnowledgeCrystal(60f).Any())
        {
            idleTime = 0;
            SetRuntimeStatus("知識バフを更新しています。");
            Plugin.Chain.Submit(ChainHelper.ReturnChain(new ReturnChainConfig
            {
                ForceReturn = false,
                ApproachAetheryte = true,
                ApplyBuffs = true,
            }));
            return;
        }

        if (baseCamp.DistanceToPlayer() <= AethernetData.DISTANCE)
        {
            idleTime = 0;
            return;
        }

        // Near base camp, walk to the waiting point. Demi-Déjion is forbidden
        // in this area even if the character is not yet beside the aetheryte.
        if (ZoneData.IsNearBaseCamp())
        {
            idleTime = 0;
            Plugin.Chain.Submit(ChainHelper.PathfindToAndWait(baseCamp.Position, AethernetData.DISTANCE));
            return;
        }

        idleTime += framework.UpdateDelta.TotalMilliseconds;
        if (idleTime > 250)
        {
            idleTime = 0;

            Svc.Log.Info($"No active CE or FATE: returning to {baseCamp.Aethernet.ToFriendlyString()}.");
            Plugin.Chain.Submit(ChainHelper.ReturnChain(new ReturnChainConfig
            {
                ForceReturn = true,
                ApproachAetheryte = true,
            }));
        }
    }

    private CriticalEncounter? FindCriticalEncounter(AutomatorModule module, Lifestream lifestream, VNavmesh vnav)
    {
        if (!module.TryGetModule<CriticalEncountersModule>(out var source) || source == null)
        {
            return null;
        }

        foreach (var encounter in source.CriticalEncounters.Values)
        {
            if (!IsCriticalEncounterEnabled(module, encounter.DynamicEventId))
            {
                continue;
            }

            if (IsActivityCoolingDown(EventType.CriticalEncounter, encounter.DynamicEventId))
            {
                continue;
            }

            if (encounter.State != DynamicEventState.Register)
            {
                continue;
            }

            return new CriticalEncounter(EventData.GetCriticalEncounter(encounter.DynamicEventId), lifestream, vnav, module, source);
        }

        return null;
    }

    private Activity? FindActivityByPriority(AutomatorModule module, Lifestream lifestream, VNavmesh vnav)
    {
        foreach (var priority in module.Config.GetPriorityOrder())
        {
            Activity? candidate = priority switch
            {
                AutomationPriority.MagicPot when module.Config.ShouldDoFates =>
                    FindFate(module, lifestream, vnav, FateSelection.MagicPot),
                AutomationPriority.CriticalEncounter when module.Config.ShouldDoCriticalEncounters =>
                    FindCriticalEncounter(module, lifestream, vnav),
                AutomationPriority.Fate when module.Config.ShouldDoFates =>
                    FindFate(module, lifestream, vnav, FateSelection.Ordinary),
                _ => null,
            };

            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private FateActivity? FindFate(
        AutomatorModule module,
        Lifestream lifestream,
        VNavmesh vnav,
        FateSelection selection = FateSelection.Any)
    {
        if (!module.TryGetModule<FatesModule>(out var source) || source == null)
        {
            return null;
        }

        var candidates = selection == FateSelection.Any && Svc.Objects.LocalPlayer != null
            ? source.fates.Values
                .OrderBy(fate => Vector3.Distance(Svc.Objects.LocalPlayer.Position, fate.StartPosition) <= fate.Radius ? 0 : 1)
                .ThenBy(fate => Vector3.Distance(Svc.Objects.LocalPlayer.Position, fate.StartPosition))
            : source.fates.Values.OrderBy(fate => fate.Id);

        foreach (var fate in candidates)
        {
            if (fate.CurrentProgress >= 100)
            {
                continue;
            }

            if (!IsFateEnabled(module, fate.Id))
            {
                continue;
            }

            if (IsActivityCoolingDown(EventType.Fate, fate.Id))
            {
                continue;
            }

            var isMagicPot = fate.IsPotFate() || NorthHornContent.IsMagicPotFate(fate.Id);
            if (selection == FateSelection.MagicPot && !isMagicPot)
            {
                continue;
            }

            if (selection == FateSelection.Ordinary && isMagicPot)
            {
                continue;
            }

            return new FateActivity(fate.Data, lifestream, vnav, module, fate);
        }

        return null;
    }

    private static string GetMonitoringStatus(AutomatorModule module)
    {
        var fateCount = module.TryGetModule<FatesModule>(out var fates) && fates != null
            ? fates.fates.Values.Count(fate => fate.CurrentProgress < 100 && IsFateEnabled(module, fate.Id))
            : 0;
        var criticalCount = module.TryGetModule<CriticalEncountersModule>(out var critical) && critical != null
            ? critical.CriticalEncounters.Values.Count(encounter =>
                encounter.State == DynamicEventState.Register &&
                IsCriticalEncounterEnabled(module, encounter.DynamicEventId))
            : 0;

        return $"アクティビティ監視中（FATE {fateCount}件／CE {criticalCount}件）";
    }

    private static bool IsCriticalEncounterEnabled(AutomatorModule module, uint eventId)
    {
        if (module.Config.CriticalEncountersMap.TryGetValue(eventId, out var enabled))
        {
            return enabled;
        }

        // North Horn's ordinary CEs are 49-63. The following IDs are Forked
        // Tower events and must not be handled as normal travel activities.
        return ZoneData.IsInNorthHorn() &&
               eventId is >= 49 and <= 63 &&
               module.Config.IsNorthCriticalEncounterEnabled(eventId);
    }

    private static bool IsFateEnabled(AutomatorModule module, uint fateId)
    {
        if (module.Config.FatesMap.TryGetValue(fateId, out var enabled))
        {
            return enabled;
        }

        return ZoneData.IsInNorthHorn() &&
               fateId is >= 2072 and <= 2084 &&
               module.Config.IsNorthFateEnabled(fateId);
    }

    private bool UpdateActivityProgressWatchdog(
        AutomatorModule module,
        Lifestream lifestream,
        VNavmesh vnav,
        TeleporterController teleporter)
    {
        var activity = Activity;
        if (activity == null)
        {
            ResetActivityWatchdog();
            return false;
        }

        if (!ReferenceEquals(watchedActivity, activity))
        {
            watchedActivity = activity;
            watchedActivityState = activity.state;
            lastActivityPosition = Player.Position;
            lastActivityDistance = activity.DistanceToDestination();
            lastActivityProgressUtc = DateTime.UtcNow;
            activityRecoveryAttempts = 0;
            return false;
        }

        if (watchedActivityState != activity.state)
        {
            watchedActivityState = activity.state;
            lastActivityPosition = Player.Position;
            lastActivityDistance = activity.DistanceToDestination();
            lastActivityProgressUtc = DateTime.UtcNow;
        }

        if (activity.state != ActivityState.Pathfinding)
        {
            return false;
        }

        var inAreaTransition = Svc.Condition[ConditionFlag.BetweenAreas] ||
                               Svc.Condition[ConditionFlag.BetweenAreas51];
        if (inAreaTransition || Player.IsCasting ||
            (LifestreamIpc.TryIsBusy(lifestream, out var lifestreamBusy) && lifestreamBusy))
        {
            lastActivityProgressUtc = DateTime.UtcNow;
            return false;
        }

        var position = Player.Position;
        var distance = activity.DistanceToDestination();
        if (Vector3.Distance(position, lastActivityPosition) >= 1.5f ||
            distance <= lastActivityDistance - 1f)
        {
            lastActivityPosition = position;
            lastActivityDistance = distance;
            lastActivityProgressUtc = DateTime.UtcNow;
            return false;
        }

        if (DateTime.UtcNow - lastActivityProgressUtc <= TimeSpan.FromSeconds(20))
        {
            return false;
        }

        activityRecoveryAttempts++;
        VnavmeshIpc.TryCancelAllPathfinds(vnav);
        VnavmeshIpc.TryStop(vnav);
        LifestreamIpc.TryAbort(lifestream);
        Plugin.Chain.Abort();

        if (activityRecoveryAttempts >= 3)
        {
            var key = GetActivityKey(activity.data.Type, activity.data.Id);
            activityCooldowns[key] = DateTime.UtcNow.AddMinutes(1);
            var name = activity.GetName();
            Activity = null;
            idleTime = 0;
            ResetActivityWatchdog();
            if (module.Config.ShouldToggleAiProvider)
            {
                module.Config.AiProvider.Off();
            }

            teleporter.RequestMandatoryCompletionReturn($"{name}への移動が3回停止");
            SetRuntimeStatus($"{name}への移動を一時除外し、拠点へ復帰します。");
            Svc.Log.Warning($"Automation travel stalled three times; {name} is cooling down for 1 minute.");
            return true;
        }

        activity.ResetPathfindingForRecovery();
        lastActivityPosition = Player.Position;
        lastActivityDistance = activity.DistanceToDestination();
        lastActivityProgressUtc = DateTime.UtcNow;
        SetRuntimeStatus($"{activity.GetName()}への移動停止を検知し、経路を再作成します（{activityRecoveryAttempts}/3）。");
        Svc.Log.Warning($"Automation travel stalled; rebuilding route ({activityRecoveryAttempts}/3) for {activity.GetName()}.");
        return true;
    }

    private bool IsActivityCoolingDown(EventType type, uint id)
    {
        var key = GetActivityKey(type, id);
        if (!activityCooldowns.TryGetValue(key, out var until))
        {
            return false;
        }

        if (until > DateTime.UtcNow)
        {
            return true;
        }

        activityCooldowns.Remove(key);
        return false;
    }

    private static string GetActivityKey(EventType type, uint id)
    {
        return $"{type}:{id}";
    }

    private void ResetActivityWatchdog()
    {
        watchedActivity = null;
        lastActivityPosition = default;
        lastActivityDistance = float.MaxValue;
        lastActivityProgressUtc = DateTime.MinValue;
        activityRecoveryAttempts = 0;
    }

    private void CompleteActivity(Activity completedActivity, AutomatorModule module, VNavmesh vnav)
    {
        EndTrackedActivity(module, vnav);

        var name = string.IsNullOrWhiteSpace(completedActivity.data.InternalName)
            ? "アクティビティ"
            : completedActivity.data.InternalName;
        module.GetModule<TeleporterModule>().teleporter.RequestMandatoryCompletionReturn($"{name}完了");
        SetRuntimeStatus($"{name}の完了・消失を確認しました。必須帰還へ移行します。");
    }

    internal bool CompleteTrackedActivityFromExternal(AutomatorModule module, string reason)
    {
        if (Activity == null)
        {
            return false;
        }

        module.TryGetIPCSubscriber<VNavmesh>(out var vnav);
        EndTrackedActivity(module, vnav);
        module.GetModule<TeleporterModule>().teleporter.RequestMandatoryCompletionReturn(reason);
        SetRuntimeStatus($"{reason}を確認しました。宝箱探索後に必須帰還を実行します。");
        return true;
    }

    private void EndTrackedActivity(AutomatorModule module, VNavmesh? vnav)
    {
        Plugin.Chain.Abort();
        if (!Svc.Condition[ConditionFlag.InCombat])
        {
            VnavmeshIpc.TryStop(vnav);
            if (module.Config.ShouldToggleAiProvider)
            {
                module.Config.AiProvider.Off();
            }
        }

        Activity = null;
        idleTime = 0;
        ResetActivityWatchdog();
    }

    private static bool IsProtectedBuffSequenceActive()
    {
        var current = Plugin.Chain.CurrentChain;
        if (current == null)
        {
            // A newly submitted mode-start chain is visible in the queue one
            // frame before it becomes CurrentChain. Preserve that frame too.
            return Plugin.Chain.QueueCount > 0;
        }

        if (current.Name is "TankyushinAtKnowledgeCrystal" or "TankyushinActivationChain" or "AllBuffsChain")
        {
            return true;
        }

        // ReturnChain may currently contain its nested buff sequence. Once it
        // has switched to すっぴん, let the chain restore the captured job
        // before an activity is allowed to replace it.
        return current.Name is "ReturnChain" && Job.Current.id == JobId.Freelancer;
    }

    private static bool ShouldWaitForMagicPot(
        AutomatorModule module,
        MagicPotModule? magicPot,
        StateManagerModule states)
    {
        if (magicPot == null ||
            !magicPot.IsEnabled ||
            !ZoneData.IsInNorthHorn() ||
            !module.Config.ShouldDoFates ||
            magicPot.IsNorthPotActive ||
            (!module.Config.IsNorthFateEnabled(2072) && !module.Config.IsNorthFateEnabled(2073)))
        {
            return false;
        }

        if (module.automator.Activity?.state == ActivityState.Participating ||
            states.GetState() is State.InFate or State.InCriticalEncounter)
        {
            return false;
        }

        var remaining = GetMagicPotRemaining(magicPot);
        return remaining is { } value &&
               value >= TimeSpan.FromMinutes(-2) &&
               value < TimeSpan.FromMinutes(5);
    }

    private static TimeSpan? GetMagicPotRemaining(MagicPotModule? magicPot)
    {
        return magicPot?.NextSpawnUtc is { } next ? next - DateTime.UtcNow : null;
    }

    private static string FormatRemaining(TimeSpan? remaining)
    {
        if (remaining == null)
        {
            return "計算中";
        }

        var value = remaining.Value < TimeSpan.Zero ? TimeSpan.Zero : remaining.Value;
        return $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
    }

    public IReadOnlyList<string> GetExecutionPlan(AutomatorModule module)
    {
        var dependencies = AutomationDependencies.GetSnapshot();
        if (!dependencies.AllReady)
        {
            return
            [
                $"必須プラグイン不足によりCIUT自動処理を停止：{string.Join("、", dependencies.MissingNames)}",
                "vnavmesh・BMR・RSRをすべてロード",
                "vnavmeshの現在エリア用ナビメッシュ準備を確認",
                "RSR IPCの登録完了を確認",
                "準備完了後に自動操作と選択済み機能を再評価",
            ];
        }

        if (!module.IsEnabled)
        {
            return
            [
                "自動操作モードは停止中です",
                "自動操作を開始して実行許可を与える",
                "CE移動・FATE移動・トレジャーハントを個別に選択",
                "選択した機能だけを監視・実行",
                "戦闘中はCIUTを停止して戦闘AIへ操作を委譲",
            ];
        }

        if (module.IsSuspendedForCombat)
        {
            return
            [
                "戦闘を検知：CIUTの自動操作を停止",
                "vnavmesh・対象選択・帰還操作を戦闘AIへ委譲",
                "現在のFATE・CE・宝箱進行を保持",
                "戦闘終了を待機",
                "戦闘終了後に保持した処理を再開",
            ];
        }

        if (!module.HasSelectedOperation)
        {
            return
            [
                "自動操作の実行許可は有効です",
                "CE移動を開始するか選択",
                "FATE移動を開始するか選択",
                "トレジャーハントを実施するか選択",
                "選択されるまで移動・帰還・バフ操作を行いません",
            ];
        }

        if (module.TryGetModule<MagicPotModule>(out var activeMagicPot) &&
            activeMagicPot?.IsTreasureSearchActive == true)
        {
            return activeMagicPot.GetTreasureExecutionPlan();
        }

        if (Plugin.Chain.CurrentChain?.Name == "TankyushinAtKnowledgeCrystal")
        {
            if (module.IsAutomatedTreasureHuntRunning())
            {
                return
                [
                    "トレジャーハント開始要求を実行中",
                    "デミデジョンで北部ベースキャンプへ帰還",
                    "ナレッジクリスタル付近でたんきゅうしんを実行",
                    "エーテライト付近でマギ・トレジャーサーチを実行",
                    "内部データの未確認座標から固定巡回経路を生成",
                ];
            }

            return
            [
                "ナレッジクリスタル付近へ移動",
                "移動停止とマウント解除を確認",
                "すっぴんへ変更してたんきゅうしんを実行",
                "付与対象バフを確認",
                "元のサポートジョブへ復帰して選択済み処理を再評価",
            ];
        }

        var teleporter = module.GetModule<TeleporterModule>().teleporter;
        if (teleporter.IsCompletionReturnPending)
        {
            return teleporter.GetCompletionExecutionPlan();
        }

        if (waitingForMagicPot)
        {
            return
            [
                "現在の移動・待機処理を停止",
                "マジックポットFATEの発生を待機",
                "発生を検知して対象に設定",
                "最寄りエーテライトから発生地点へ移動",
                "FATEへ参加し完了を監視",
            ];
        }

        if (Activity != null)
        {
            var name = Activity.GetName();
            var distance = Math.Max(0f, Activity.DistanceToDestination());
            return Activity.state switch
            {
                ActivityState.Idle =>
                [$"{name}を移動対象として確定", "現在の移動処理を停止", "対象に最も近い利用可能エーテライトを決定", "魔導通路で転送", $"発生地点まで約 {distance:F0}mを経路移動"],
                ActivityState.Pathfinding =>
                [$"{name}へ経路移動中（残り約 {distance:F0}m）", "対象範囲への到着を確認", $"{name}へ参加", "完了または消失を監視", "完了・消失時にデミデジョンで拠点へ帰還"],
                ActivityState.Participating =>
                [$"{name}へ参加中", "戦闘中はCIUT操作を停止してBMR・RSRへ委譲", "完了または消失を検知", "デミデジョンで拠点へ帰還", "エーテライト付近で選択済み処理を再評価"],
                _ =>
                [$"{name}の完了処理を開始", "移動と対象処理を停止", "デミデジョンを実行", "北部ベースキャンプのエーテライト付近へ移動", "選択済み処理を再評価"],
            };
        }

        if (module.IsAutomatedTreasureHuntRunning())
        {
            var treasure = module.GetModule<TreasureModule>();
            return treasure.Hunter.GetExecutionPlan();
        }

        var order = string.Join(" → ", module.Config.GetPriorityOrder().Select(priority => priority.ToJapaneseLabel()));
        return
        [
            RuntimeStatus,
            $"優先順位を適用：{order}",
            "発生中の選択済みFATE・CEを再取得",
            "マジックポット予想が5分未満か再判定",
            "対象がなければ現在エリアのベースキャンプ・エーテライト付近で待機",
        ];
    }

    private enum FateSelection
    {
        Any,
        MagicPot,
        Ordinary,
    }

    public void Refresh()
    {
        Activity = null;
        idleTime = 0;
        waitingForMagicPot = false;
        ResetActivityWatchdog();
    }

    public void ResumeAfterCombatSuspension()
    {
        idleTime = 0;
        ResetActivityWatchdog();
    }
}
