namespace Ocelot.Modules;

public abstract partial class Module<P, C>
    where P : OcelotPlugin
    where C : IOcelotConfig
{
    public virtual bool ShouldInitialize
    {
        get => IsEnabled;
    }

    public virtual void PreInitialize()
    {
    }

    public virtual void Initialize()
    {
    }

    public virtual void PostInitialize()
    {
    }
}
