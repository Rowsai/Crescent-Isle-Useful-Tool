using System;
using System.Collections.Generic;
using ECommons.EzIpcManager;
using Ocelot.Chain;
using Ocelot.IPC;

namespace Ocelot.Debug;

public class DebugIPCSubscriber(string identifier) : IPCSubscriber($"{identifier}.Debug")
{
    [EzIPC] public readonly Func<string> GetOcelotVersion = null!;

    [EzIPC] public readonly Func<string> GetPluginVersion = null!;

    [EzIPC] public readonly Func<string> GetPluginName = null!;

    [EzIPC] public readonly Func<Dictionary<string, QueueInfo>> GetChainQueueInfo = null!;

    [EzIPC] public readonly Action AbortAllChains = null!;
}
