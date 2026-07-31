using FFXIVClientStructs.FFXIV.Client.Game;

namespace Ocelot.Gameplay;

public partial class Actions
{
    public class Paladin
    {
        public readonly static Action IronWill = new(ActionType.Action, 28);

        public readonly static Action ReleaseIronWill = new(ActionType.Action, 32065);
    }
}
