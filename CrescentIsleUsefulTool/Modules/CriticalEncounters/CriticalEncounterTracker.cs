using System;
using System.Collections.Generic;
using System.Linq;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Modules.Fates;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace CrescentIsleUsefulTool.Modules.CriticalEncounters;

public class CriticalEncounterTracker
{
    public Dictionary<uint, CriticalEncounterSnapshot> CriticalEncounters { get; private set; } = new();

    public Dictionary<uint, EventProgress> Progress { get; } = new();

    public TowerTimer TowerTimer { get; private set; }

    // Store last known states of each event by ID
    private readonly Dictionary<uint, DynamicEventState> lastStates = new();

    public CriticalEncounterTracker(CriticalEncountersModule module)
    {
        TowerTimer = new TowerTimer(this, module.GetModule<FatesModule>());
    }

    public event Action<CriticalEncounterSnapshot>? OnInactiveState;

    public event Action<CriticalEncounterSnapshot>? OnRegisterState;

    public event Action<CriticalEncounterSnapshot>? OnWarmupState;

    public event Action<CriticalEncounterSnapshot>? OnBattleState;


    public unsafe void Tick(IFramework _)
    {
        var content = PublicContentOccultCrescent.GetInstance();
        if (content == null || !ZoneData.IsInOccultCrescent())
        {
            Reset();
            return;
        }

        var snapshots = new Dictionary<uint, CriticalEncounterSnapshot>();
        foreach (var ev in content->DynamicEventContainer.Events.ToArray())
        {
            var id = (uint)ev.DynamicEventId;
            if (id == 0)
            {
                continue;
            }

            snapshots[id] = new CriticalEncounterSnapshot(
                id,
                ev.State,
                (byte)ev.Progress,
                (uint)ev.EventType,
                (uint)ev.StartTimestamp,
                ev.MapMarker.Position,
                EventData.GetCriticalEncounterDisplayName(id));
        }

        CriticalEncounters = snapshots;

        foreach (var ev in CriticalEncounters.Values)
        {
            // Get previous state, default to Inactive if unknown
            lastStates.TryGetValue(ev.DynamicEventId, out var previousState);

            var currentState = ev.State;

            if (currentState == DynamicEventState.Battle)
            {
                if (ev.Progress == 0)
                {
                    continue;
                }

                if (!Progress.TryGetValue(ev.DynamicEventId, out var progress))
                {
                    progress = new EventProgress();
                    Progress[ev.DynamicEventId] = progress;
                }

                if (progress.samples.Count == 0 || progress.samples[^1].Progress != ev.Progress)
                {
                    progress.Add(ev.Progress);
                }

                if (ev.Progress == 100)
                {
                    Progress.Remove(ev.DynamicEventId);
                }
            }
            else
            {
                Progress.Remove(ev.DynamicEventId);
            }

            if (previousState == currentState)
            {
                continue;
            }

            lastStates[ev.DynamicEventId] = currentState;

            switch (currentState)
            {
                case DynamicEventState.Inactive:
                    OnInactiveState?.Invoke(ev);
                    break;

                case DynamicEventState.Register:
                    OnRegisterState?.Invoke(ev);
                    break;

                case DynamicEventState.Warmup:
                    OnWarmupState?.Invoke(ev);
                    break;

                case DynamicEventState.Battle:
                    OnBattleState?.Invoke(ev);
                    break;
            }
        }

        foreach (var staleId in lastStates.Keys.Except(CriticalEncounters.Keys).ToArray())
        {
            lastStates.Remove(staleId);
            Progress.Remove(staleId);
        }
    }

    public void Reset()
    {
        CriticalEncounters.Clear();
        Progress.Clear();
        lastStates.Clear();
    }
}
