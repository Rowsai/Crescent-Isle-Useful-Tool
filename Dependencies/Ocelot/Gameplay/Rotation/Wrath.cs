using System;
using ECommons.DalamudServices;
using Ocelot.IPC;
using Ocelot.Modules;

namespace Ocelot.Gameplay.Rotation;

public class Wrath : IRotationPlugin
{
    private readonly WrathCombo wrath;

    private readonly Guid leaseId;

    public Wrath(IModule module)
    {
        wrath = module.GetIPCSubscriber<WrathCombo>();
        var lease = wrath.RegisterForLease(Svc.PluginInterface.InternalName, module.GetType().FullName!);
        if (lease == null)
        {
            throw new Exception("Unable to create Wrath Combo");
        }

        leaseId = (Guid)lease;
    }

    public void DisableAoe()
    {
        // wrath.SetAutoRotationConfigState(leaseId, option, null);
    }

    void IDisposable.Dispose()
    {
        wrath.ReleaseControl(leaseId);
    }
}
