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
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Chain;
using Ocelot.IPC;

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
            SetRuntimeStatus("完了したFATE・CEからデミデジョンで拠点へ帰還しています。");
            return;
        }

        var forceMagicPotSelection = false;
        if (ShouldWaitForMagicPot(module, magicPot, states))
        {
            if (!waitingForMagicPot)
            {
                waitingForMagicPot = true;
                Activity = null;
                idleTime = 0;
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
            if (states.GetState() == State.InCombat)
            {
                SetRuntimeStatus("戦闘状態の終了を待っています。");
                return;
            }

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
                module.Debug($"Resuming running activity: {Activity.GetName()}");
                return;
            }

            if (states.GetState() == State.InFate)
            {
                Activity = FindFate(module, lifestream, vnav);
                if (Activity != null)
                {
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
            SetRuntimeStatus("たんきゅうしんを実行し、元のサポートジョブへ戻しています。");
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
                Plugin.Chain.Abort();
                VnavmeshIpc.TryStop(vnav);
                Svc.Log.Info($"Selected priority activity: {Activity.GetName()}");
                SetRuntimeStatus($"{Activity.GetName()}を検知しました。移動を開始します。");
            }
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

    private static CriticalEncounter? FindCriticalEncounter(AutomatorModule module, Lifestream lifestream, VNavmesh vnav)
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

            if (encounter.State != DynamicEventState.Register)
            {
                continue;
            }

            return new CriticalEncounter(EventData.GetCriticalEncounter(encounter.DynamicEventId), lifestream, vnav, module, source);
        }

        return null;
    }

    private static Activity? FindActivityByPriority(AutomatorModule module, Lifestream lifestream, VNavmesh vnav)
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

    private static FateActivity? FindFate(
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

    private void CompleteActivity(Activity completedActivity, AutomatorModule module, VNavmesh vnav)
    {
        Plugin.Chain.Abort();
        VnavmeshIpc.TryStop(vnav);
        if (module.Config.ShouldToggleAiProvider)
        {
            module.Config.AiProvider.Off();
        }

        Activity = null;
        idleTime = 0;

        var name = string.IsNullOrWhiteSpace(completedActivity.data.InternalName)
            ? "Activity"
            : completedActivity.data.InternalName;
        module.GetModule<TeleporterModule>().teleporter.RequestMandatoryCompletionReturn($"{name}完了");
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

        if (current.Name is "TankyushinActivationChain" or "AllBuffsChain")
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
        if (!module.IsEnabled)
        {
            return
            [
                "自動操作モードは停止中です",
                "開始時にたんきゅうしんを実行",
                "設定した優先順位でFATE・CEを監視",
                "対象発生時に最寄りエーテライトへ移動",
                "完了後にデミデジョンで拠点へ帰還",
            ];
        }

        var teleporter = module.GetModule<TeleporterModule>().teleporter;
        if (teleporter.IsCompletionReturnPending)
        {
            return
            [
                "移動を停止してデミデジョンを実行",
                "拠点への帰還を確認",
                "マギ・トレジャーサーチで残数を更新",
                "必要ならたんきゅうしんでバフを更新",
                "エーテライト付近へ移動して監視再開",
            ];
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
            return Activity.state switch
            {
                ActivityState.Idle =>
                ["現在の移動処理を整理", "最寄りエーテライトを決定", "エーテライトで移動", "発生地点へ経路移動", "参加・完了を監視"],
                ActivityState.Pathfinding =>
                ["発生地点へ経路移動", "対象範囲への到着を確認", "FATE・CEへ参加", "完了を監視", "デミデジョンで拠点へ帰還"],
                ActivityState.Participating =>
                ["参加中のFATE・CEを継続", "完了または消滅を検知", "移動・戦闘支援を停止", "デミデジョンで拠点へ帰還", "エーテライト付近で監視再開"],
                _ =>
                ["完了処理を開始", "デミデジョンを実行", "拠点へ帰還", "エーテライト付近へ移動", "監視を再開"],
            };
        }

        var order = string.Join(" → ", module.Config.GetPriorityOrder().Select(priority => priority.ToJapaneseLabel()));
        return
        [
            $"優先順位を適用：{order}",
            "有効なFATE・CEを監視",
            "マジックポット予想が5分未満か確認",
            "対象がなければ拠点へ帰還",
            "エーテライト付近で待機を継続",
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
    }
}
