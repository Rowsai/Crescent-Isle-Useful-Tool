using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.CriticalEncounters;
using CrescentIsleUsefulTool.Modules.StateManager;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Chain;
using Ocelot.IPC;
using System;
using System.Linq;
using System.Numerics;

namespace CrescentIsleUsefulTool.Modules.Automator;

public class CriticalEncounter : Activity
{
    private readonly CriticalEncountersModule source;

    private bool finalDestination = false;

    public CriticalEncounter(EventData data, Lifestream lifestream, VNavmesh vnav, AutomatorModule module, CriticalEncountersModule source)
        : base(data, lifestream, vnav, module)
    {
        this.source = source;

        handlers.Add(ActivityState.WaitingToStartCriticalEncounter, GetWaitingToStartCriticalEncounterChain);
    }

    protected override TaskManagerTask GetPathfindingWatcher(StateManagerModule states)
    {
        return new TaskManagerTask(() =>
        {
            if (!IsValid())
            {
                throw new Exception("Activity is no longer valid.");
            }

            if (!finalDestination && IsCloseToZone())
            {
                // Get all players in the zone
                var playersInZone = Svc.Objects
                    .Where(o => o.ObjectKind == ObjectKind.Pc)
                    .Where(o => Vector3.Distance(o.Position, GetPosition()) <= (data.Radius ?? GetRadius()))
                    .ToList();

                if (playersInZone.Count > 4)
                {
                    var minX = playersInZone.Min(p => p.Position.X);
                    var maxX = playersInZone.Max(p => p.Position.X);
                    var minY = playersInZone.Min(p => p.Position.Z);
                    var maxY = playersInZone.Max(p => p.Position.Z);

                    // Choose a random point within the bounding box of players
                    var rand = new Random();
                    var randX = (float)(minX + rand.NextDouble() * (maxX - minX));
                    var randY = (float)(minY + rand.NextDouble() * (maxY - minY));
                    var randomPoint = new Vector3(randX, GetPosition().Y, randY);

                    module.Debug($"Pathfinding to random point: {randomPoint} (MinX: {minX}, MaxX: {maxX}, MinY: {minY}, MaxY: {maxY})");

                    VnavmeshIpc.TryPathfindAndMoveTo(vnav, randomPoint, false);
                    finalDestination = true;
                }
            }

            if (!finalDestination && IsInZone())
            {
                VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
                if (isRunning)
                {
                    VnavmeshIpc.TryStop(vnav);
                }

                return true;
            }

            if (!source.CriticalEncounters.TryGetValue(data.Id, out var encounter))
            {
                throw new Exception("The critical encounter is no longer available.");
            }

            if (encounter.State != DynamicEventState.Register)
            {
                throw new Exception("This event started without you");
            }

            if (finalDestination)
            {
                VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
                return !isRunning;
            }

            VnavmeshIpc.TryIsRunning(vnav, out var pathRunning);
            if (!pathRunning)
            {
                throw new VnavmeshStoppedException();
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = 180000, ShowError = false });
    }


    private Func<Chain> GetWaitingToStartCriticalEncounterChain(StateManagerModule states)
    {
        return () =>
        {
            return Chain.Create("Automation:WaitingToStartCriticalEncounter")
                .Then(new TaskManagerTask(() =>
                    {
                        if (!IsValid())
                        {
                            throw new Exception("The critical encounter appears to have started without you.");
                        }

                        if (!source.CriticalEncounters.TryGetValue(data.Id, out var encounter))
                        {
                            throw new Exception("The critical encounter is no longer available.");
                        }

                        if (encounter.State == DynamicEventState.Battle &&
                            states.GetState() != State.InCriticalEncounter)
                        {
                            throw new Exception("The critical encounter appears to have started without you.");
                        }

                        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
                        if (!isRunning && states.GetState() == State.InCombat)
                        {
                            Actions.TryUnmount();

                            if (module.Config.ShouldToggleAiProvider)
                            {
                                module.Config.AiProvider.On();
                            }
                        }

                        return states.GetState() == State.InCriticalEncounter;
                    },
                    new TaskManagerConfiguration
                    {
                        TimeLimitMS = 180000,
                    }))
                .Then(_ => SetState(ActivityState.Participating));
        };
    }

    public override unsafe bool IsValid()
    {
        if (!source.CriticalEncounters.TryGetValue(data.Id, out var encounter))
        {
            return false;
        }

        if (encounter.State == DynamicEventState.Register)
        {
            return true;
        }

        var dec = DynamicEventContainer.GetInstance();
        return dec != null && encounter.DynamicEventId == dec->CurrentEventId;
    }

    protected override float GetRadius()
    {
        // This is kind of an assumption, but it seems accurate enough for most encounters.
        // return Encounter.Unknown4;
        return 19f;
    }

    protected override Vector3 GetPosition()
    {
        return source.CriticalEncounters.TryGetValue(data.Id, out var encounter)
            ? encounter.Position
            : Player.Position;
    }

    public override string GetName()
    {
        return EventData.GetCriticalEncounterDisplayName(data.Id);
    }

    private bool IsCloseToZone(float radius = 50f)
    {
        return Player.DistanceTo(GetPosition()) <= radius;
    }


    protected override bool IsActivityTarget(IBattleNpc obj)
    {
        return obj.SubKind == (byte)BattleNpcSubKind.Combatant
               && obj.IsValid()
               && Vector3.Distance(obj.Position, GetPosition()) <= GetRadius() + 15f;
    }

    protected override ActivityState GetPostPathfindingState()
    {
        return ActivityState.WaitingToStartCriticalEncounter;
    }
}
