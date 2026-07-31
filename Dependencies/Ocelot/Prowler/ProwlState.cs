namespace Ocelot.Prowler;

public enum ProwlState
{
    NotStarted,

    Pathfinding,

    Mounting,

    Moving,

    Complete,

    Cancelled,

    Redirecting,
}
