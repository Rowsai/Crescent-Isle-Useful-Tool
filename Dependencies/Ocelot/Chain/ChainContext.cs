using System.Threading;

namespace Ocelot.Chain;

public class ChainContext
{
    public CancellationTokenSource source { get; } = new();

    public CancellationToken token
    {
        get => source.Token;
    }
}
