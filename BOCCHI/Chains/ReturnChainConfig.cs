namespace BOCCHI.Chains;

public struct ReturnChainConfig()
{
    public bool ApproachAetheryte { get; init; } = true;

    public bool ForceReturn { get; init; } = false;

    /// <summary>
    /// Refresh knowledge buffs while returning. Priority activity travel turns
    /// this off so a nearby crystal cannot delay movement to a FATE or CE.
    /// </summary>
    public bool ApplyBuffs { get; init; } = true;

    /// <summary>
    /// Cast Treasuresight after a completed automated activity so that the
    /// tracker receives the current bronze and silver coffer counts.
    /// </summary>
    public bool UpdateTreasureCount { get; init; } = false;
}
