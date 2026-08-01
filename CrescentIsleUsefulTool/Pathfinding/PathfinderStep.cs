using CrescentIsleUsefulTool.Enums;
using System.Numerics;

namespace CrescentIsleUsefulTool.Pathfinding;

public class PathfinderStep
{
    public PathfinderStepType Type;

    public uint NodeId = 0;

    public Aethernet Aethernet = Aethernet.BaseCamp;

    public Vector3 Position;

    public Vector3 ArrivalPosition;

    public static PathfinderStep WalkToDestination(uint id)
    {
        return new PathfinderStep
        {
            Type = PathfinderStepType.WalkToNode,
            NodeId = id,
        };
    }

    public static PathfinderStep WalkToAethernet(Aethernet aethernet)
    {
        return new PathfinderStep
        {
            Type = PathfinderStepType.WalkToAethernet,
            Aethernet = aethernet,
        };
    }

    public static PathfinderStep TeleportToAethernet(Aethernet aethernet)
    {
        return new PathfinderStep
        {
            Type = PathfinderStepType.TeleportToAethernet,
            Aethernet = aethernet,
        };
    }

    public static PathfinderStep ReturnToBaseCamp()
    {
        return new PathfinderStep
        {
            Type = PathfinderStepType.ReturnToBaseCamp,
        };
    }

    public static PathfinderStep RideTurbulence(Vector3 position, Vector3 arrivalPosition)
    {
        return new PathfinderStep
        {
            Type = PathfinderStepType.RideTurbulence,
            Position = position,
            ArrivalPosition = arrivalPosition,
        };
    }
}
