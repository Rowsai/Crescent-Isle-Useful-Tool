using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BOCCHI.Data;
using BOCCHI.Enums;
using BOCCHI.Pathfinding;
using ECommons.DalamudServices;

namespace BOCCHI.Modules.Treasure;

/// <summary>
/// Builds a North Horn treasure route from the coffer placements loaded by the
/// game.  Unlike South Horn, North Horn does not rely on a bundled precomputed
/// path file, so newly added placements continue to be discovered automatically.
/// </summary>
public sealed class NorthHornPathfinder(
    IReadOnlyCollection<TreasureData.TreasureDatum> treasure,
    float teleportCost = 50f) : IPathfinder
{
    private const float TeleportSavingThreshold = 25f;

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

        State = PathfinderState.PathfindingDone;
        Svc.Log.Info($"North Horn treasure route built from internal layout data: {treasureById.Count} locations, {steps.Count} steps.");
        return Task.FromResult(steps);
    }

    private TravelPlan BuildTravelPlan(Vector3 from, TreasureData.TreasureDatum treasure)
    {
        var destination = treasure.Position;
        var directCost = Vector3.Distance(from, destination);
        var directSteps = new List<PathfinderStep> { PathfinderStep.WalkToDestination(treasure.Id) };

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
            PathfinderStep.WalkToDestination(treasure.Id),
        ]);
    }

    private readonly record struct TravelPlan(float Cost, List<PathfinderStep> Steps);
}
