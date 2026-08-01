using System.Numerics;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Ocelot;
using Ocelot.Windows;

namespace CrescentIsleUsefulTool.Modules.Automator;

[OcelotWindow]
public class AutomatorWindow(Plugin _plugin, Config _config) : OcelotWindow(_plugin, _config)
{
    private bool windowThemePushed;

    public override void PreDraw()
    {
        base.PreDraw();
        CrescentTheme.PushWindowChrome();
        windowThemePushed = true;
    }

    public override void PostDraw()
    {
        if (windowThemePushed)
        {
            CrescentTheme.PopWindowChrome();
            windowThemePushed = false;
        }

        base.PostDraw();
    }

    public override void PostInitialize()
    {
        base.PostInitialize();

        TitleBarButtons.Add(new TitleBarButton
        {
            Click = (m) =>
            {
                if (m != ImGuiMouseButton.Left)
                {
                    return;
                }

                AutomatorModule.ToggleAutomationMode(Plugin);
            },
            Icon = FontAwesomeIcon.Robot,
            IconColor = CrescentTheme.AccentSoft,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip("自動操作モードを切り替え"),
        });
    }

    protected override void Render(RenderContext context)
    {
        using var theme = CrescentTheme.Push();

        if (!ZoneData.IsInOccultCrescent())
        {
            ImGui.TextUnformatted(I18N.T("generic.label.not_in_zone"));
            return;
        }

        var automator = Plugin.Modules.GetModule<AutomatorModule>();
        if (!automator.IsEnabled)
        {
            ImGui.TextUnformatted("自動操作モードはOFFです。");
            return;
        }

        automator.panel.Draw(automator);
    }

    protected override string GetWindowName()
    {
        return Plugin.Modules.GetModule<AutomatorModule>().T("panel.lens.title");
    }
}
