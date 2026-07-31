using System;
using Dalamud.Interface.Windowing;

namespace Ocelot.Windows;

public abstract class OcelotWindow : Window, IDisposable
{
    protected readonly OcelotPlugin Plugin;

    protected readonly IOcelotConfig PluginConfig;

    public OcelotWindow(OcelotPlugin plugin, IOcelotConfig pluginConfig)
        : base("")
    {
        Plugin = plugin;
        PluginConfig = pluginConfig;

        WindowName = GetWindowName();
        I18N.OnLanguageChanged += (oldLang, newLang) => { WindowName = GetWindowName(); };
    }

    protected abstract void Render(RenderContext context);

    public override void Draw()
    {
        if (Plugin.RenderContext == null)
        {
            return;
        }

        Render(Plugin.RenderContext);
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

    public void ToggleOrExpand()
    {
        // if (IsCollapsed)
        // {
        //     Collapsed = false;
        //     return;
        // }

        IsOpen = !IsOpen;
    }


    public virtual void Dispose()
    {
    }

    protected abstract string GetWindowName();
}
