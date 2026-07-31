using System;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using Ocelot.Extensions;

namespace Ocelot.Gameplay;

public static class TankHelper
{
    public readonly static Paladin Paladin = new();

    public readonly static Warrior Warrior = new();

    public readonly static DarkKnight DarkKnight = new();

    public readonly static Gunbreaker Gunbreaker = new();

    public static bool IsTank
    {
        get => Current != null;
    }

    public static ITank? Current
    {
        get => Player.Job.GetData().RowId switch
        {
            1 or 19 => Paladin,
            3 or 21 => Warrior,
            32 => DarkKnight,
            37 => Gunbreaker,
            _ => null,
        };
    }
}

public interface ITank
{
    bool HasStanceOn();

    Func<Chain.Chain> TurnStanceOn();

    Func<Chain.Chain> TurnStanceOff();
}

public abstract class BaseTank(uint statusId) : ITank
{
    public bool HasStanceOn()
    {
        return Player.Status.HasStatus(statusId);
    }

    public abstract Func<Chain.Chain> TurnStanceOn();

    public abstract Func<Chain.Chain> TurnStanceOff();
}

public class Paladin() : BaseTank(79)
{
    public override Func<Chain.Chain> TurnStanceOn()
    {
        return Actions.Paladin.IronWill.GetCastChain();
    }

    public override Func<Chain.Chain> TurnStanceOff()
    {
        return Actions.Paladin.ReleaseIronWill.GetCastChain();
    }
}

public class Warrior() : BaseTank(91)
{
    public override Func<Chain.Chain> TurnStanceOn()
    {
        return Actions.Warrior.Defiance.GetCastChain();
    }

    public override Func<Chain.Chain> TurnStanceOff()
    {
        return Actions.Warrior.ReleaseDefiance.GetCastChain();
    }
}

public class DarkKnight() : BaseTank(743)
{
    public override Func<Chain.Chain> TurnStanceOn()
    {
        return Actions.DarkKnight.Grit.GetCastChain();
    }

    public override Func<Chain.Chain> TurnStanceOff()
    {
        return Actions.DarkKnight.ReleaseGrit.GetCastChain();
    }
}

public class Gunbreaker() : BaseTank(1833)
{
    public override Func<Chain.Chain> TurnStanceOn()
    {
        return Actions.Gunbreaker.RoyalGuard.GetCastChain();
    }

    public override Func<Chain.Chain> TurnStanceOff()
    {
        return Actions.Gunbreaker.ReleaseRoyalGuard.GetCastChain();
    }
}
