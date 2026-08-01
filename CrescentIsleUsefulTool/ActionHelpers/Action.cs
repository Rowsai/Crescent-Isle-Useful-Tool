using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using Ocelot.Chain;

namespace CrescentIsleUsefulTool.ActionHelpers;

public unsafe class Action(ActionType type, uint id)
{
    public float GetRecastTime()
    {
        var manager = ActionManager.Instance();
        if (manager == null)
        {
            return float.MaxValue;
        }

        var recast = manager->GetRecastTime(type, id);
        var elapsed = manager->GetRecastTimeElapsed(type, id);

        return recast - elapsed;
    }

    public bool CanCast()
    {
        return GetRecastTime() <= 0f;
    }

    public void Cast()
    {
        var manager = ActionManager.Instance();
        if (manager != null)
        {
            manager->UseAction(type, id);
        }
    }

    public void Cast(uint arg)
    {
        var manager = ActionManager.Instance();
        if (manager != null)
        {
            manager->UseAction(type, id, arg);
        }
    }

    public Func<Chain> GetCastChain()
    {
        return () => CastOnChain(Chain.Create($"Action({type}, {id})"));
    }

    public Chain CastOnChain(Chain chain)
    {
        return chain
            .Then(_ => CanCast())
            .Then(_ => Cast());
    }
}
