using System;

namespace Ocelot.Chain.ChainEx;

public static class ChainRunIf
{
    public static Chain RunIf(this Chain chain, Func<bool> predicate)
    {
        return chain.Then(context =>
        {
            if (!predicate())
            {
                Logger.Debug("Cancelling Chain");
                context.source.Cancel();
            }
        });
    }

    public static Chain BreakIf(this Chain chain, Func<bool> predicate)
    {
        return chain.Then(context =>
        {
            if (predicate())
            {
                Logger.Debug("Cancelling Chain");
                context.source.Cancel();
            }
        });
    }
}
