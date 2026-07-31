using ECommons.EzIpcManager;
using ECommons.Reflection;

namespace Ocelot.IPC;

public abstract class IPCSubscriber
{
    protected readonly string Identifier;

    protected IPCSubscriber(string identifier)
    {
        Identifier = identifier;
        EzIPC.Init(this, identifier);
    }

    public bool IsReady()
    {
        return DalamudReflector.TryGetDalamudPlugin(Identifier, out _, false, true);
    }
}
