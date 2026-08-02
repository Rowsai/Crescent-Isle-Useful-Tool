namespace CrescentIsleUsefulTool.Modules.Automator;

public enum AutomationPriority
{
    MagicPot = 0,
    CriticalEncounter = 1,
    Fate = 2,
}

public static class AutomationPriorityExtensions
{
    public static string ToJapaneseLabel(this AutomationPriority priority)
    {
        return priority switch
        {
            AutomationPriority.MagicPot => "マジックポット",
            AutomationPriority.CriticalEncounter => "クリティカルエンカウント",
            AutomationPriority.Fate => "FATE",
            _ => "不明",
        };
    }
}
