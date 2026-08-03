using FFXIVClientStructs.FFXIV.Client.Game;

namespace CrescentIsleUsefulTool.ActionHelpers;

public static partial class Actions
{
    public static class Freelancer
    {
        public static Action Resuscitation { get; private set; } = new(ActionType.GeneralAction, 31);

        // GeneralAction 32 resolves to Action row 41651. Invoke the concrete
        // action so measurement requests and action hooks use the same ID.
        public static Action Treasuresight { get; private set; } = new(ActionType.Action, 41651);

        // GeneralAction 33 resolves to Action row 46606. Invoke the concrete
        // action row so logs and action hooks consistently report ID 46606.
        public static Action Tankyushin { get; private set; } = new(ActionType.Action, 46606);
    }
}
