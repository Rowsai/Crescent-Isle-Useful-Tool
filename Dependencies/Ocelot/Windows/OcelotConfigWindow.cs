namespace Ocelot.Windows;

public abstract class OcelotConfigWindow(OcelotPlugin plugin, IOcelotConfig pluginConfig) : OcelotWindow(plugin, pluginConfig)
{
    protected override void Render(RenderContext context)
    {
        Plugin.Modules.RenderConfigUi(context);
    }

    protected override string GetWindowName()
    {
        return $"{I18N.T("windows.config.title")}##Config";
    }
}
