namespace BOCCHI.Chains;

public struct ReturnChainConfig()
{
    public bool ApproachAetheryte { get; init; } = true;

    public bool ForceReturn { get; init; } = false;

    /// <summary>
    /// Cast Treasuresight after a completed automated activity so that the
    /// tracker receives the current bronze and silver coffer counts.
    /// </summary>
    public bool UpdateTreasureCount { get; init; } = false;
}
