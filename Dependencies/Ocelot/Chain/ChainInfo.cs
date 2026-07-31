using System;

namespace Ocelot.Chain;

public class ChainInfo
{
    public required string Name { get; init; }

    public TimeSpan TimeAlive { get; init; }

    public float Progress { get; init; }

    public int TotalLinks { get; init; }

    public int CompletedLinks { get; init; }

    private ChainInfo()
    {
    }

    public static ChainInfo? FromChain(Chain? chain)
    {
        if (chain == null)
        {
            return null;
        }

        return new ChainInfo
        {
            Name = chain.Name,
            TimeAlive = chain.TimeAlive,
            Progress = chain.Progress,
            TotalLinks = chain.TotalLinks,
            CompletedLinks = chain.CompletedLinks,
        };
    }
}
