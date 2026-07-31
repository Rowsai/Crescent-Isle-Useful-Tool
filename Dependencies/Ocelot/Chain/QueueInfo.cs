using System;

namespace Ocelot.Chain;

public class QueueInfo
{
    public TimeSpan TimeAlive { get; init; }

    public int ChainsCompleted { get; init; }

    public ChainInfo? CurrentChain { get; init; }


    public static QueueInfo FromChainQueue(ChainQueue queue)
    {
        return new QueueInfo
        {
            TimeAlive = queue.TimeAlive,
            ChainsCompleted = queue.ChainsCompleted,
            CurrentChain = ChainInfo.FromChain(queue.CurrentChain),
        };
    }
}
