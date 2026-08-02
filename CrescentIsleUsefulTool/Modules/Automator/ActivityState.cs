namespace CrescentIsleUsefulTool.Modules.Automator;

public enum ActivityState
{
    Idle,
    Pathfinding,
    WaitingToStartCriticalEncounter,
    Participating,
    Done,
}

public static class ActivityStateExtensions
{
    public static string ToLabel(this ActivityState state)
    {
        return state switch
        {
            ActivityState.Idle => "開始準備中",
            ActivityState.Pathfinding => "目的地へ移動中",
            ActivityState.WaitingToStartCriticalEncounter => "CE開始待機中",
            ActivityState.Participating => "参加中",
            ActivityState.Done => "完了",
            _ => "状態不明",
        };
    }
}
