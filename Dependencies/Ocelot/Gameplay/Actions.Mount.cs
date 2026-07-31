using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Ocelot.Gameplay;

public partial class Actions
{
    public static Action Roulette
    {
        get => Actions.MountRoulette;
    }

    public static Action Mount(uint id)
    {
        return new Action(ActionType.Mount, id);
    }

    public readonly static Action Unmount = new(ActionType.Mount, 0);

    public static void TryUnmount()
    {
        if (Svc.Condition[ConditionFlag.Mounted])
        {
            Unmount.Cast();
        }
    }
}
