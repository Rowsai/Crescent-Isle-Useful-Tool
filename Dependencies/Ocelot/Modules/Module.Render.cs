using Ocelot.Windows;

namespace Ocelot.Modules;

public abstract partial class Module<P, C>
    where P : OcelotPlugin
    where C : IOcelotConfig
{
    public virtual bool ShouldRender
    {
        get => IsEnabled;
    }

    public WindowManager Windows
    {
        get => Plugin.Windows;
    }

    public virtual void Render(RenderContext context)
    {
    }

    public virtual bool RenderMainUi(RenderContext context)
    {
        return false;
    }

    public virtual void RenderConfigUi(RenderContext context)
    {
        if (Config != null && Config.Draw())
        {
            PluginConfig.Save();
        }
    }

    public T GetWindow<T>() where T : OcelotWindow
    {
        return Windows.GetWindow<T>();
    }

    public bool TryGetWindow<T>(out T? provider) where T : OcelotWindow
    {
        return Windows.TryGetWindow(out provider);
    }
}
