using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Pathfinding;
using Data_TreasureData = CrescentIsleUsefulTool.Data.TreasureData;

namespace CrescentIsleUsefulTool.Modules.Treasure;

using TreasureData = (uint id, Vector3 position, uint type);

public class Pathfinder : BasePathfinder
{
    private readonly List<Data_TreasureData.TreasureDatum> treasure;

    public Pathfinder(List<Data_TreasureData.TreasureDatum> treasure, float returnCost = 300f, float teleportCost = 50f) : base(returnCost, teleportCost)
    {
        this.treasure = treasure;

        LoadFile("precomputed_treasure_hunt_data.json");
    }

    protected override uint GetStartingNode(Vector3 start, List<uint> nodes)
    {
        var closestDistance = float.MaxValue;
        var startTreasure = treasure.First();
        foreach (var treasureData in treasure)
        {
            if (!nodes.Contains(treasureData.Id))
            {
                continue;
            }

            var distance = Vector3.Distance(start, treasureData.Position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                startTreasure = treasureData;
            }
        }

        return startTreasure.Id;
    }

    // Treasure hunting follows a simple nearest-neighbour route.  This keeps the
    // first destination closest to the player's current position and visits each
    // subsequent coffer from the last one visited, instead of reordering the route
    // with a global TSP insertion pass.
    protected override List<uint> SolveTSPNearestInsertion(
        uint start,
        List<uint> nodes,
        Dictionary<uint, Dictionary<uint, (float Cost, List<PathfinderStep> Steps)>> graph)
    {
        var route = new List<uint> { start };
        var unvisited = new HashSet<uint>(nodes);
        unvisited.Remove(start);
        var current = start;

        while (unvisited.Count > 0)
        {
            var next = unvisited
                .OrderBy(node => graph[current][node].Cost)
                .First();

            route.Add(next);
            unvisited.Remove(next);
            current = next;
        }

        return route;
    }
}
