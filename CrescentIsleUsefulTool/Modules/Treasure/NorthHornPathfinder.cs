using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Pathfinding;
using ECommons.DalamudServices;

namespace CrescentIsleUsefulTool.Modules.Treasure;

/// <summary>
/// Builds a North Horn treasure route from the coffer placements loaded by the
/// game. The southwest plateau is deliberately traversed through the planmap's
/// turbulence pairs because it is not connected to the surrounding navmesh.
/// </summary>
public sealed class NorthHornPathfinder(
    IReadOnlyCollection<TreasureData.TreasureDatum> treasure,
    float teleportCost = 50f) : IPathfinder
{
    private const float TeleportSavingThreshold = 25f;

    // LVD_zone_01 EventRange trigger and paired PopRange landing positions
    // from North Horn's planmap.lgb. Each pair provides an up and down route.
    private static readonly TurbulencePair WestTurbulence = new(
        new Vector3(-833.534f, 97.653f, 553.106f),
        new Vector3(-913.125f, 157.800f, 631.422f),
        new Vector3(-900.858f, 157.800f, 629.249f),
        new Vector3(-822.230f, 94.347f, 543.300f));

    private static readonly TurbulencePair EastTurbulence = new(
        new Vector3(-471.645f, 96.432f, 885.058f),
        new Vector3(-502.207f, 158.663f, 880.599f),
        new Vector3(-502.410f, 158.576f, 894.453f),
        new Vector3(-452.753f, 96.348f, 886.693f));

    private readonly Dictionary<uint, TreasureData.TreasureDatum> treasureById = treasure
        .GroupBy(item => item.Id)
        .ToDictionary(group => group.Key, group => group.First());

    public PathfinderState State { get; private set; } = PathfinderState.FileLoaded;

    public Task<List<PathfinderStep>> FindPath(Vector3 _, List<uint> nodes)
    {
        if (State != PathfinderState.FileLoaded)
        {
            throw new InvalidOperationException("North Horn treasure coordinates are not ready.");
        }

        State = PathfinderState.Pathfinding;

        var remaining = nodes
            .Where(treasureById.ContainsKey)
            .Distinct()
            .ToHashSet();
        var plateauNodes = remaining
            .Where(id => IsSouthwestTurbulencePlateau(treasureById[id].Position))
            .ToHashSet();
        remaining.ExceptWith(plateauNodes);

        var steps = new List<PathfinderStep>
        {
            // Treasure hunting in North Horn always begins at the expedition base.
            PathfinderStep.ReturnToBaseCamp(),
        };

        var current = Aethernet.NorthBaseCamp.GetData().Position;
        while (remaining.Count > 0)
        {
            var next = remaining
                .Select(id => (Id: id, Plan: BuildTravelPlan(current, treasureById[id])))
                .OrderBy(candidate => candidate.Plan.Cost)
                .ThenBy(candidate => candidate.Id)
                .First();

            steps.AddRange(next.Plan.Steps);
            current = treasureById[next.Id].Position;
            remaining.Remove(next.Id);
        }

        if (plateauNodes.Count > 0)
        {
            var westToEast = BuildPlateauPlan(current, plateauNodes, WestTurbulence, EastTurbulence);
            var eastToWest = BuildPlateauPlan(current, plateauNodes, EastTurbulence, WestTurbulence);
            var plateauPlan = westToEast.Cost <= eastToWest.Cost ? westToEast : eastToWest;
            steps.AddRange(plateauPlan.Steps);
        }

        State = PathfinderState.PathfindingDone;
        Svc.Log.Info(
            $"North Horn treasure route built from internal layout data: {treasureById.Count} locations, " +
            $"{plateauNodes.Count} turbulence-plateau locations, {steps.Count} steps.");
        return Task.FromResult(steps);
    }

    private TravelPlan BuildPlateauPlan(
        Vector3 from,
        IReadOnlyCollection<uint> nodes,
        TurbulencePair entry,
        TurbulencePair exit)
    {
        var entryTravel = BuildTravelPlan(
            from,
            entry.LowerTrigger,
            PathfinderStep.RideTurbulence(entry.LowerTrigger, entry.UpperLanding));
        var (orderedNodes, plateauCost) = FindShortestPlateauOrder(entry.UpperLanding, exit.UpperTrigger, nodes);

        var steps = new List<PathfinderStep>(entryTravel.Steps);
        steps.AddRange(orderedNodes.Select(PathfinderStep.WalkToDestination));
        steps.Add(PathfinderStep.RideTurbulence(exit.UpperTrigger, exit.LowerLanding));
        return new TravelPlan(entryTravel.Cost + plateauCost, steps);
    }

    private (List<uint> Nodes, float Cost) FindShortestPlateauOrder(
        Vector3 start,
        Vector3 exit,
        IReadOnlyCollection<uint> nodes)
    {
        var bestOrder = new List<uint>();
        var bestCost = float.MaxValue;

        void Search(Vector3 current, HashSet<uint> remaining, List<uint> order, float cost)
        {
            if (remaining.Count == 0)
            {
                var total = cost + Vector3.Distance(current, exit);
                if (total < bestCost)
                {
                    bestCost = total;
                    bestOrder = [.. order];
                }

                return;
            }

            foreach (var id in remaining.ToArray())
            {
                var destination = treasureById[id].Position;
                var nextCost = cost + Vector3.Distance(current, destination);
                if (nextCost >= bestCost)
                {
                    continue;
                }

                remaining.Remove(id);
                order.Add(id);
                Search(destination, remaining, order, nextCost);
                order.RemoveAt(order.Count - 1);
                remaining.Add(id);
            }
        }

        Search(start, nodes.ToHashSet(), [], 0f);
        return (bestOrder, bestCost);
    }

    private TravelPlan BuildTravelPlan(Vector3 from, TreasureData.TreasureDatum treasure)
    {
        return BuildTravelPlan(from, treasure.Position, PathfinderStep.WalkToDestination(treasure.Id));
    }

    private TravelPlan BuildTravelPlan(Vector3 from, Vector3 destination, PathfinderStep finalStep)
    {
        var directCost = Vector3.Distance(from, destination);
        var directSteps = new List<PathfinderStep> { finalStep };

        var aethernets = AethernetData.All().ToList();
        var source = aethernets.MinBy(data => Vector3.Distance(from, data.Position));
        var target = aethernets.MinBy(data => Vector3.Distance(destination, data.Destination));

        if (source == null || target == null || source.Aethernet == target.Aethernet)
        {
            return new TravelPlan(directCost, directSteps);
        }

        var teleportRouteCost = Vector3.Distance(from, source.Position)
                                + teleportCost
                                + Vector3.Distance(target.Destination, destination);
        if (teleportRouteCost + TeleportSavingThreshold >= directCost)
        {
            return new TravelPlan(directCost, directSteps);
        }

        return new TravelPlan(teleportRouteCost,
        [
            PathfinderStep.WalkToAethernet(source.Aethernet),
            PathfinderStep.TeleportToAethernet(target.Aethernet),
            finalStep,
        ]);
    }

    private static bool IsSouthwestTurbulencePlateau(Vector3 position)
    {
        return position is { X: < -450f, Y: > 145f, Z: > 500f };
    }

    private readonly record struct TravelPlan(float Cost, List<PathfinderStep> Steps);

    private readonly record struct TurbulencePair(
        Vector3 LowerTrigger,
        Vector3 UpperLanding,
        Vector3 UpperTrigger,
        Vector3 LowerLanding);
}
