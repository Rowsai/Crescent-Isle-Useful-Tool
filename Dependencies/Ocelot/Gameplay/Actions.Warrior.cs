using FFXIVClientStructs.FFXIV.Client.Game;

namespace Ocelot.Gameplay;

public partial class Actions
{
    public class Warrior
    {
        public readonly static Action Defiance = new(ActionType.Action, 48);

        public readonly static Action ReleaseDefiance = new(ActionType.Action, 32066);
    }
}
