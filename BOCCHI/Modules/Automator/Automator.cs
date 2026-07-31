using System.Linq;
using BOCCHI.Chains;
using BOCCHI.Data;
using BOCCHI.Enums;
using BOCCHI.Modules.CriticalEncounters;
using BOCCHI.Modules.Fates;
using BOCCHI.Modules.StateManager;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Chain;
using Ocelot.IPC;

namespace BOCCHI.Modules.Automator;

public class Automator
{
    private static bool IsChainActive
    {
        get => ChainManager.Queues.Count > 0;
    }

    public Activity? Activity { get; private set; } = null;

    private int idleTime = 0;

    public void PostUpdate(AutomatorModule module, IFramework framework)
    {
        var vnav = module.GetIPCSubscriber<VNavmesh>();
        var lifestream = module.GetIPCSubscriber<Lifestream>();
        if (!vnav.IsReady() || !lifestream.IsReady())
        {
            return;
        }

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
                var encounter = critical.CriticalEncounters.Values.Last(ev => ev.State != DynamicEventState.Inactive);
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
            var completedCriticalEncounter = Activity.data.Type == EventType.CriticalEncounter;
            Activity = null;
            idleTime = 0;

            if (completedCriticalEncounter)
            {
                Svc.Log.Info("Critical Encounter complete: casting Demi Dejon.");
                Plugin.Chain.Submit(ChainHelper.ReturnChain(new ReturnChainConfig { ForceReturn = true }));
            }

            return;
        }

        if (Activity != null && !Activity.IsValid())
        {
            Plugin.Chain.Abort();
            vnav.Stop();
            Activity = null;
        }

        // CE/FATE destinations take precedence over an already queued utility
        // chain (for example, returning to an aetheryte).  Selecting them before
        // the chain guard lets illegal mode immediately start travelling to the
        // event's target point when it appears.
        if (Activity == null)
        {
            Activity = module.Config.ShouldDoCriticalEncounters ? FindCriticalEncounter(module, lifestream, vnav) : null;
            Activity ??= module.Config.ShouldDoFates ? FindFate(module, lifestream, vnav) : null;
            if (Activity != null)
            {
                idleTime = 0;
                Plugin.Chain.Abort();
                vnav.Stop();
                Svc.Log.Info($"Selected priority activity: {Activity.GetName()}");
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
            return;
        }

        if (!module.Config.ShouldDoFates && !module.Config.ShouldDoCriticalEncounters)
        {
            return;
        }

        // A CE can already be preparing or in progress by the time the
        // automator evaluates it. Do not cast Return while the player is
        // walking there manually or waiting for registration to reopen.
        if (HasActiveCriticalEncounter(module))
        {
            idleTime = 0;
            return;
        }

        var baseCamp = (ZoneData.IsInNorthHorn() ? Aethernet.NorthBaseCamp : Aethernet.BaseCamp).GetData();
        if (baseCamp.DistanceToPlayer() <= AethernetData.DISTANCE)
        {
            idleTime = 0;
            return;
        }

        idleTime += framework.UpdateDelta.Milliseconds;
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

    private static FateActivity? FindFate(AutomatorModule module, Lifestream lifestream, VNavmesh vnav)
    {
        if (!module.TryGetModule<FatesModule>(out var source) || source == null)
        {
            return null;
        }

        foreach (var fate in source.fates.Values)
        {
            if (!IsFateEnabled(module, fate.Id))
            {
                continue;
            }

            return new FateActivity(fate.Data, lifestream, vnav, module, fate);
        }

        return null;
    }

    private static bool IsCriticalEncounterEnabled(AutomatorModule module, uint eventId)
    {
        if (module.Config.CriticalEncountersMap.TryGetValue(eventId, out var enabled))
        {
            return enabled;
        }

        // North Horn's ordinary CEs are 49-63. The following IDs are Forked
        // Tower events and must not be handled as normal travel activities.
        return ZoneData.IsInNorthHorn() && eventId is >= 49 and <= 63;
    }

    private static bool IsFateEnabled(AutomatorModule module, uint fateId)
    {
        if (module.Config.FatesMap.TryGetValue(fateId, out var enabled))
        {
            return enabled;
        }

        return ZoneData.IsInNorthHorn() && fateId is >= 2072 and <= 2084;
    }

    private static bool HasActiveCriticalEncounter(AutomatorModule module)
    {
        return module.TryGetModule<CriticalEncountersModule>(out var source)
            && source != null
            && source.CriticalEncounters.Values.Any(encounter => encounter.State != DynamicEventState.Inactive);
    }

    public void Refresh()
    {
        Activity = null;
        idleTime = 0;
    }
}
