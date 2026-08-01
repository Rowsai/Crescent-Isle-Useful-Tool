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

    // A completion return is a single operation: cast Demi-Déjion at the
    // activity, arrive at base camp, then approach the aetheryte.  Keeping
    // this state prevents the idle fallback from casting it a second time
    // beside the aetheryte.
    private bool returningAfterActivity = false;

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
        if (Activity == null)
        {
            if (states.GetState() == State.InCombat)
            {
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
                    return;
                }

                var encounter = encounters[^1];
                var data = EventData.GetCriticalEncounter(encounter.DynamicEventId);
                Activity = new CriticalEncounter(data, lifestream, vnav, module, critical);

                if (Activity != null)
                {
                    module.Debug($"Resuming running activity: {Activity.GetName()}");
                }

                return;
            }

            if (states.GetState() == State.InFate)
            {
                Activity ??= FindFate(module, lifestream, vnav);

                if (Activity != null)
                {
                    module.Debug($"Resuming running activity: {Activity.GetName()}");
                }

                return;
            }
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
            if (Activity.HasParticipated)
            {
                CompleteActivity(Activity, module, vnav);
                return;
            }

            Plugin.Chain.Abort();
            VnavmeshIpc.TryStop(vnav);
            Activity = null;
        }

        // CE/FATE destinations take precedence over an already queued utility
        // chain (for example, returning to an aetheryte).  Selecting them before
        // the chain guard lets automation mode immediately start travelling to the
        // event's target point when it appears.
        if (Activity == null)
        {
            // Explicit priority: Magic Pot > Critical Encounter > ordinary FATE.
            Activity = module.Config.ShouldDoFates ? FindFate(module, lifestream, vnav, FateSelection.MagicPot) : null;
            Activity ??= module.Config.ShouldDoCriticalEncounters ? FindCriticalEncounter(module, lifestream, vnav) : null;
            Activity ??= module.Config.ShouldDoFates ? FindFate(module, lifestream, vnav, FateSelection.Ordinary) : null;
            if (Activity != null)
            {
                idleTime = 0;
                returningAfterActivity = false;
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

        if (returningAfterActivity)
        {
            if (IsChainActive)
            {
                return;
            }

            if (ZoneData.IsNearBaseCamp())
            {
                returningAfterActivity = false;
                return;
            }

            // The return operation did not arrive at base camp (for example,
            // it was cancelled externally).  Let the normal idle recovery
            // retry after its delay instead of issuing duplicate casts.
            returningAfterActivity = false;
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
        if (idleTime > 3000)
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

        if (returningAfterActivity || ZoneData.IsNearBaseCamp())
        {
            return;
        }

        returningAfterActivity = true;
        var name = string.IsNullOrWhiteSpace(completedActivity.data.InternalName)
            ? "Activity"
            : completedActivity.data.InternalName;
        Svc.Log.Info($"{name} complete: casting Demi Dejon once and returning to base camp.");
        Plugin.Chain.Submit(ChainHelper.ReturnChain(new ReturnChainConfig
        {
            ForceReturn = true,
            WaitForStationaryDemiReturn = true,
            ApproachAetheryte = true,
            UpdateTreasureCount = true,
        }));
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
        returningAfterActivity = false;
    }
}
