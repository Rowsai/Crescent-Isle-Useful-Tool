using ECommons.DalamudServices;

namespace Ocelot.Modules;

public abstract partial class Module<P, C>
    where P : OcelotPlugin
    where C : IOcelotConfig
{
    public void Debug(string log)
    {
        Svc.Log.Debug($"[{GetType().Name}] {log}");
    }

    public void Error(string log)
    {
        Svc.Log.Error($"[{GetType().Name}] {log}");
    }

    public void Fatal(string log)
    {
        Svc.Log.Fatal($"[{GetType().Name}] {log}");
    }

    public void Info(string log)
    {
        Svc.Log.Info($"[{GetType().Name}] {log}");
    }

    public void Verbose(string log)
    {
        Svc.Log.Verbose($"[{GetType().Name}] {log}");
    }

    public void Warning(string log)
    {
        Svc.Log.Warning($"[{GetType().Name}] {log}");
    }

    public void Warn(string log)
    {
        Svc.Log.Warning($"[{GetType().Name}] {log}");
    }
}
