using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using Ocelot.Extensions;
using Ocelot.Gameplay;
using Ocelot.IPC;

namespace Ocelot.Prowler;

public class Prowl(Vector3 destination, IGameObject? obj = null)
{
    public ProwlState State { get; private set; } = ProwlState.NotStarted;

    public Vector3 OriginalDestination { get; private set; } = destination;

    public Vector3 FinalDestination { get; private set; } = destination;

    public Vector3 Destination
    {
        get => FinalDestination;
        set => FinalDestination = value;
    }

    private ulong? gameObjectEntityId = obj?.EntityId;

    public IGameObject? GameObject => gameObjectEntityId.HasValue
        ? Svc.Objects.FirstOrDefault(candidate => candidate.EntityId == gameObjectEntityId.Value)
        : null;

    public readonly Vector3 OriginalStart = Player.Position;

    public Vector3 FinalStart { get; set; } = Player.Position;

    public Vector3 Start
    {
        get => FinalStart;
        set => FinalStart = value;
    }

    public Func<Prowl, bool> ShouldFly { get; init; } = _ => true;

    public Func<Prowl, bool> ShouldMount { get; init; } = _ => !Svc.Condition[ConditionFlag.InCombat];

    public uint Mount { get; init; } = 0;

    public Func<Prowl, bool> ShouldSprint { get; init; } = _ => true;

    public Func<Prowl, bool> ShouldTrack { get; init; } = _ => false;

    public List<Vector3> OriginalNodes { get; private set; } = [];

    public List<Vector3> FinalNodes { get; private set; } = [];


    public List<Vector3> Nodes
    {
        get => FinalNodes;
        set => FinalNodes = value;
    }

    public Action<Prowl> PreProcessor { get; init; } = _ => { };

    public Action<Prowl> PostProcessor { get; init; } = _ => { };

    public Func<Prowl, bool> Watcher { get; init; } = _ => false;

    public Action<Prowl, VNavmesh> OnComplete { get; init; } = (prowl, vnavmesh) => { };

    public Action<Prowl, VNavmesh> OnCancel { get; init; } = (prowl, vnavmesh) => { };

    private Task<List<Vector3>>? pathfindingTask = null;

    private Task<List<Vector3>>? trackingTask = null;

    public float EuclideanDistance
    {
        get => Vector3.Distance(Start, Destination);
    }

    public float OriginalPathLength
    {
        get => OriginalNodes.Length();
    }

    public float PathLength
    {
        get => Nodes.Length();
    }

    private static ChainQueue ChainQueue
    {
        get => ChainManager.Get("Prolwer.Prowl.ChainQueue");
    }

    public Prowl(IGameObject gameObject)
        : this(gameObject.Position)
    {
        gameObjectEntityId = gameObject.EntityId;
    }

    public Func<Chain.Chain> GetChain(VNavmesh vnavmesh)
    {
        return () => Chain.Chain.Create($"Prowl({Start:f2}, {Destination:f2})")
            .Debug("Waiting for vnavmesh not to be running")
            .Then(_ => VNavmeshSafe.TryIsRunning(vnavmesh, out var running) && !running)
            .Debug("Running preprocessor")
            .Then(_ => PreProcessor.Invoke(this))
            .Debug("Starting pathfinding task")
            .Then(_ => VNavmeshSafe.TryPathfind(vnavmesh, Start, Destination, ShouldFly(this), out pathfindingTask))
            .Then(_ => State = ProwlState.Pathfinding)
            .BreakIf(() => pathfindingTask == null)
            .Debug("Waiting for pathfinding to be done")
            .Then(_ => VNavmeshSafe.TryIsRunning(vnavmesh, out var running) && !running && pathfindingTask!.IsCompleted)
            .Then(_ =>
            {
                if (pathfindingTask!.IsCanceled)
                {
                    Logger.Debug("Pathfinding task cancelled");
                }

                if (pathfindingTask.IsFaulted)
                {
                    Logger.Debug("Pathfinding task faulted");
                }
            })
            .BreakIf(() => pathfindingTask!.IsCanceled || pathfindingTask!.IsFaulted)
            .Debug("Getting pathfinding result")
            .Then(_ =>
            {
                OriginalNodes = new List<Vector3>(pathfindingTask!.Result);
                Nodes = new List<Vector3>(pathfindingTask!.Result);
            })
            .Debug("Running postprocessor")
            .Then(_ => PostProcessor.Invoke(this))
            .ConditionalThen(_ => ShouldMount(this) && !Player.Mounted, _ =>
            {
                State = ProwlState.Mounting;

                if (Mount == 0)
                {
                    Actions.MountRoulette.Cast();
                }
                else
                {
                    Actions.Mount(Mount).Cast();
                }
            })
            .Then(_ =>
            {
                if (!ShouldMount(this) || Player.Mounted)
                {
                    return true;
                }

                if (!Player.IsCasting || !Player.Mounting)
                {
                    throw new Exception("Failed to mount");
                }

                return false;
            })
            .ConditionalThen(_ => !ShouldFly(this) && ShouldSprint(this) && Actions.Sprint.CanCast() && !Player.Mounted, _ => Actions.Sprint.Cast())
            .Debug("Following Path")
            .Then(_ => VNavmeshSafe.TryMoveTo(vnavmesh, Nodes, ShouldFly(this)))
            .Then(_ => State = ProwlState.Moving)
            .Then(_ =>
            {
                if (!VNavmeshSafe.TryIsRunning(vnavmesh, out var running) || !running || Watcher.Invoke(this))
                {
                    return true;
                }

                var gameObject = GameObject;
                if (gameObject == null)
                {
                    return false;
                }

                if (!ShouldTrack(this) || trackingTask is { IsCompleted: false })
                {
                    return false;
                }

                if (trackingTask is { IsCompleted: true })
                {
                    if (trackingTask.IsCompletedSuccessfully)
                    {
                        var nodes = trackingTask.Result.Smooth().ContinueFrom(Player.Position);
                        VNavmeshSafe.TryStop(vnavmesh);
                        VNavmeshSafe.TryMoveTo(vnavmesh, nodes, false);
                    }

                    trackingTask.Dispose();
                    trackingTask = null;
                }

                if (gameObject.Position.DistanceTo2D(Destination) <= gameObject.HitboxRadius)
                {
                    return false;
                }

                var trackingDestination = gameObject.GetPointOnHitboxFromPlayer(2f);
                VNavmeshSafe.TryPathfind(vnavmesh, Player.Position, trackingDestination, false, out trackingTask);

                return false;
            })
            .Then(_ => VNavmeshSafe.TryStop(vnavmesh))
            .Then(_ =>
            {
                OnComplete.Invoke(this, vnavmesh);
                State = ProwlState.Complete;
            })
            .OnCancel(() =>
            {
                OnCancel(this, vnavmesh);
                State = ProwlState.Cancelled;
            });
    }

    public void Redirect(Vector3 destination)
    {
        Redirect(destination, null);
    }

    public void Redirect(IGameObject obj)
    {
        Redirect(obj.Position, obj);
    }

    private void Redirect(Vector3 destination, IGameObject? obj)
    {
        Prowler.Abort();
        Reset();

        State = ProwlState.Redirecting;
        OriginalDestination = destination;
        Destination = destination;
        Start = Player.Position;
        gameObjectEntityId = obj?.EntityId;

        Prowler.Prowl(this);
    }

    public void Reset()
    {
        State = ProwlState.NotStarted;
        Start = OriginalStart;
        Destination = OriginalDestination;
        OriginalNodes.Clear();
        FinalNodes.Clear();
    }
}
