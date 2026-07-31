using FFXIVClientStructs.FFXIV.Client.Game;

namespace Ocelot.Gameplay;

public partial class Actions
{
    public class Gunbreaker
    {
        public readonly static Action RoyalGuard = new(ActionType.Action, 16142);

        public readonly static Action ReleaseRoyalGuard = new(ActionType.Action, 32068);
    }
}
