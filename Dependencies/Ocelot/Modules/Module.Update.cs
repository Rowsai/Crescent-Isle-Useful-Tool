using Ocelot.Data;

namespace Ocelot.Modules;

public abstract partial class Module<P, C>
    where P : OcelotPlugin
    where C : IOcelotConfig
{
    public virtual bool ShouldUpdate
    {
        get => IsEnabled;
    }

    public virtual UpdateLimit UpdateLimit
    {
        get => UpdateLimit.None;
    }

    public virtual void PreUpdate(UpdateContext context)
    {
    }

    public virtual void Update(UpdateContext context)
    {
    }

    public virtual void PostUpdate(UpdateContext context)
    {
    }
}
