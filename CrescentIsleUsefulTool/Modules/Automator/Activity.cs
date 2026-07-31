using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.StateManager;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using Ocelot.Chain;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Modules.Automator;

public abstract class Activity
{
    private const float WillOWispVillageAreaRadius = 250f;

    public readonly EventData data;

    private readonly Lifestream lifestream;

    protected readonly VNavmesh vnav;

    protected readonly AutomatorModule module;

    public ActivityState state = ActivityState.Idle;

    public bool HasParticipated { get; private set; }

    protected readonly Dictionary<ActivityState, Func<StateManagerModule, Func<Chain>?>> handlers;

    protected Activity(EventData data, Lifestream lifestream, VNavmesh vnav, AutomatorModule module)
    {
        this.data = data;
        this.lifestream = lifestream;
        this.vnav = vnav;
        this.module = module;

        handlers = new Dictionary<ActivityState, Func<StateManagerModule, Func<Chain>?>>
        {
            { ActivityState.Idle, GetIdleChain },
            { ActivityState.Pathfinding, GetPathfindingChain },
            { ActivityState.Participating, GetParticipatingChain },
            { ActivityState.Done, GetDoneChain },
        };

        var states = module.GetModule<StateManagerModule>();
        if (states.GetState() == State.InFate || states.GetState() == State.InCriticalEncounter)
        {
            SetState(ActivityState.Participating);
        }
    }


    public Func<Chain>? GetChain(StateManagerModule states)
    {
        return !IsValid() ? null : handlers[state](states);
    }

    private Func<Chain> GetIdleChain(StateManagerModule states)
    {
        return () =>
        {
            bool ShouldToggleAi(ChainContext _)
            {
                return module.Config.ShouldToggleAiProvider && !Svc.Condition[ConditionFlag.InCombat];
            }

            return Chain.Create("Illegal:Idle")
                .ConditionalThen(ShouldToggleAi, _ => module.Config.AiProvider.Off())
                .Then(_ => { VnavmeshIpc.TryStop(vnav); })
                .Then(_ => state = ActivityState.Pathfinding);
        };
    }

    private Func<Chain> GetPathfindingChain(StateManagerModule states)
    {
        return () =>
        {
            var activityShard = GetAethernetData();
            var baseCamp = (ZoneData.IsInNorthHorn() ? Aethernet.NorthBaseCamp : Aethernet.BaseCamp).GetData();

            var isFate = data.Type == EventType.Fate;
            module.Debug($"Travelling through nearest destination aetheryte: {activityShard.Aethernet}");

            var chain = Chain.Create("Illegal:Pathfinding")
                .ConditionalWait(_ => !isFate && module.Config.ShouldDelayCriticalEncounters, Random.Shared.Next(10000, 15001))
                .ConditionalThen(_ => !ZoneData.IsNearBaseCamp(), ChainHelper.ReturnChain(new ReturnChainConfig
                {
                    ForceReturn = true,
                    ApproachAetheryte = true,
                    ApplyBuffs = false,
                }))
                .ConditionalThen(_ => baseCamp.DistanceToPlayer() > AethernetData.DISTANCE,
                    ChainHelper.PathfindToAndWait(baseCamp.Position, AethernetData.DISTANCE))
                .ConditionalThen(_ => activityShard.DistanceToPlayer() > AethernetData.DISTANCE, ChainHelper.TeleportChain(activityShard.Aethernet))
                .Debug("Waiting for lifestream to not be 'busy'")
                .Then(new TaskManagerTask(() => !lifestream.IsBusy(), new TaskManagerConfiguration { TimeLimitMS = 30000 }))
                .ConditionalThen(_ => ShouldMountToPathfindTo(GetPosition()), ChainHelper.MountChain())
                .Then(new PathfindingChain(vnav, GetPosition(), data));

            chain
                .Then(GetPathfindingWatcher(states))
                .Then(_ => SetState(GetPostPathfindingState()));

            return chain;
        };
    }


    private Func<Chain> GetParticipatingChain(StateManagerModule states)
    {
        return () =>
        {
            return Chain.Create("Illegal:Participating")
                .ConditionalThen(_ => module.Config.ShouldToggleAiProvider, _ => module.Config.AiProvider.On())
                .Then(_ => { VnavmeshIpc.TryStop(vnav); })
                .Then(new TaskManagerTask(() =>
                {
                    if (!module.Config.ShouldForceTarget || !EzThrottler.Throttle("Participating.ForceTarget", 500))
                    {
                        return states.GetState() == State.Idle;
                    }

                    var enemies = GetEnemies();
                    Svc.Targets.Target = module.Config.ShouldForceTargetCentralEnemy ? enemies.Centroid() : enemies.Closest();

                    return states.GetState() == State.Idle;
                }, new TaskManagerConfiguration { TimeLimitMS = int.MaxValue }))
                .Then(_ => SetState(ActivityState.Done));
        };
    }

    private Func<Chain>? GetDoneChain(StateManagerModule states)
    {
        return null;
    }

    protected List<IBattleNpc> GetEnemies()
    {
        return TargetHelper.Enemies.Where(IsActivityTarget).ToList();
    }

    protected abstract bool IsActivityTarget(IBattleNpc obj);

    protected void SetState(ActivityState nextState)
    {
        state = nextState;
        if (nextState == ActivityState.Participating)
        {
            HasParticipated = true;
        }
    }

    private AethernetData GetAethernetData()
    {
        var destination = GetPosition();
        var candidates = AethernetData.All();

        // The village shard can create an unnatural or obstructed route when
        // the activity is merely on the broad side of the map for which it is
        // mathematically closest. Only retain it for an activity actually in
        // the village area around the shard.
        if (ZoneData.IsInNorthHorn() && !IsInWillOWispVillageArea(destination))
        {
            candidates = candidates.Where(data => data.Aethernet != Aethernet.WillOWispVillage);
        }

        return candidates.OrderBy(data => Vector3.Distance(destination, data.Position)).First();
    }

    private static bool IsInWillOWispVillageArea(Vector3 position)
    {
        var village = Aethernet.WillOWispVillage.GetData().Position;
        var dx = position.X - village.X;
        var dz = position.Z - village.Z;
        return dx * dx + dz * dz <= WillOWispVillageAreaRadius * WillOWispVillageAreaRadius;
    }

    protected bool IsInZone()
    {
        var radius = data.Radius ?? GetRadius();

        return Player.DistanceTo(GetPosition()) <= radius;
    }

    private bool ShouldMountToPathfindTo(Vector3 destination)
    {
        if (!module.PluginConfig.TeleporterConfig.ShouldMount)
        {
            return false;
        }

        return Vector3.Distance(Player.Position, destination) > 20f;
    }

    protected abstract float GetRadius();

    protected abstract TaskManagerTask GetPathfindingWatcher(StateManagerModule states);

    public abstract bool IsValid();

    protected abstract Vector3 GetPosition();

    public abstract string GetName();

    protected abstract ActivityState GetPostPathfindingState();
}
