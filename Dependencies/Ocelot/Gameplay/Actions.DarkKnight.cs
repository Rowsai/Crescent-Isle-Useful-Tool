using FFXIVClientStructs.FFXIV.Client.Game;

namespace Ocelot.Gameplay;

public partial class Actions
{
    public class DarkKnight
    {
        public readonly static Action Grit = new(ActionType.Action, 3629);

        public readonly static Action ReleaseGrit = new(ActionType.Action, 32067);
    }
}
