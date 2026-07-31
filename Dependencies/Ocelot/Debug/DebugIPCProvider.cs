using System.Collections.Generic;
using System.Linq;
using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using Ocelot.Chain;

namespace Ocelot.Debug;

public class DebugIPCProvider
{
    private readonly OcelotPlugin plugin;

    public DebugIPCProvider(OcelotPlugin plugin)
    {
        this.plugin = plugin;

        EzIPC.Init(this, $"{Svc.PluginInterface.InternalName}.Debug");
    }

    [EzIPC]
    private string GetOcelotVersion()
    {
        return OcelotPlugin.OcelotVersion;
    }

    [EzIPC]
    private string GetPluginVersion()
    {
        return plugin.Version;
    }

    [EzIPC]
    private string GetPluginName()
    {
        return plugin.Name;
    }

    [EzIPC]
    private Dictionary<string, QueueInfo> GetChainQueueInfo()
    {
        return ChainManager.Queues.ToDictionary(p => p.Key, p => QueueInfo.FromChainQueue(p.Value));
    }

    [EzIPC]
    private void AbortAllChains()
    {
        ChainManager.AbortAll();
    }
}
