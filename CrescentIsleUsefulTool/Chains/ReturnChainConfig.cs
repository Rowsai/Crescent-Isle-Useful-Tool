namespace CrescentIsleUsefulTool.Chains;

public struct ReturnChainConfig()
{
    public bool ApproachAetheryte { get; init; } = true;

    public bool ForceReturn { get; init; } = false;

    /// <summary>
    /// Wait until movement has completely stopped before a required
    /// activity-completion return. Other callers skip Demi-Déjion when a
    /// movement route is already active, preventing an unrelated route from
    /// being interrupted by an immediately cancelled cast.
    /// </summary>
    public bool WaitForStationaryDemiReturn { get; init; } = false;

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
