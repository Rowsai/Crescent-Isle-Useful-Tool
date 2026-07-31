using System.Numerics;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Ocelot.Windows;

public abstract class OcelotMainWindow(OcelotPlugin plugin, IOcelotConfig pluginConfig) : OcelotWindow(plugin, pluginConfig)
{
    public override void PostInitialize()
    {
        if (!Plugin.Windows.TryGetWindow<OcelotConfigWindow>(out _))
        {
            return;
        }

        TitleBarButtons.Add(new TitleBarButton
        {
            Click = (m) =>
            {
                if (m != ImGuiMouseButton.Left)
                {
                    return;
                }

                Plugin.Windows.ToggleConfigUI();
            },
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(I18N.T("windows.main.buttons.toggle_config")),
        });
    }

    protected override string GetWindowName()
    {
        return $"{I18N.T("windows.main.title")}##Main";
    }
}
