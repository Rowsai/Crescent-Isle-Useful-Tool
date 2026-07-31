using System;
using ECommons.Automation.NeoTaskManager;

namespace Ocelot.Chain;

public abstract class ChainFactory
{
    protected abstract Chain Create(Chain chain);

    public virtual Func<Chain> Factory()
    {
        return () => Create(Chain.Create(GetType().Name));
    }

    public virtual TaskManagerConfiguration? Config()
    {
        return null;
    }
}
