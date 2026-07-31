using System.Numerics;
using BOCCHI.Data;
using BOCCHI.Modules.Automator;
using BOCCHI.Ui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using Ocelot;
using Ocelot.Windows;

namespace BOCCHI.Windows;

[OcelotMainWindow]
public class MainWindow(Plugin primaryPlugin, Config config) : OcelotMainWindow(primaryPlugin, config)
{
    private uint selectedTerritory;
    private uint previousTerritory;
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

                Plugin.Modules.GetModule<AutomatorModule>().DisableIllegalMode();
            },
            Icon = FontAwesomeIcon.Stop,
            IconColor = CrescentTheme.AccentSoft,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(I18N.T("windows.main.buttons.emergency_stop")),
        });

        TitleBarButtons.Add(new TitleBarButton
        {
            Click = (m) =>
            {
                if (m != ImGuiMouseButton.Left)
                {
                    return;
                }

                AutomatorModule.ToggleIllegalMode(Plugin);
            },
            Icon = FontAwesomeIcon.Skull,
            IconColor = CrescentTheme.AccentSoft,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(I18N.T("windows.main.buttons.toggle_illegal_mode")),
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

        var currentTerritory = Svc.ClientState.TerritoryType;
        var selectCurrentArea = selectedTerritory == 0 || previousTerritory != currentTerritory;
        if (selectCurrentArea)
        {
            selectedTerritory = currentTerritory;
        }

        DrawHeader(currentTerritory);

        if (ImGui.BeginTabBar("##OccultCrescentArea"))
        {
            DrawAreaTab("南征編 (South Horn)", ZoneData.SOUTHHORN, ZoneData.IsInSouthHorn(), selectCurrentArea && currentTerritory == ZoneData.SOUTHHORN, context);
            DrawAreaTab("北征編 (North Horn)", ZoneData.NORTHHORN, ZoneData.IsInNorthHorn(), selectCurrentArea && currentTerritory == ZoneData.NORTHHORN, context);
            ImGui.EndTabBar();
        }

        previousTerritory = currentTerritory;
    }

    private void DrawHeader(uint currentTerritory)
    {
        ImGui.TextColored(CrescentTheme.AccentSoft, "CRESCENT ISLE");
        ImGui.SameLine();
        ImGui.TextDisabled("USEFUL TOOL");
        ImGui.SameLine(ImGui.GetWindowWidth() - 155f);
        var area = currentTerritory == ZoneData.NORTHHORN ? "NORTH HORN" : "SOUTH HORN";
        ImGui.TextColored(CrescentTheme.Accent, $"● {area}");
        var illegalModeEnabled = Plugin.Modules.GetModule<AutomatorModule>().IsEnabled;
        ImGui.TextDisabled("不正モード:");
        ImGui.SameLine();
        ImGui.TextColored(illegalModeEnabled ? CrescentTheme.AccentSoft : CrescentTheme.Muted, illegalModeEnabled ? "ON" : "OFF");
        ImGui.Separator();
    }

    private void DrawAreaTab(string label, uint territoryId, bool isCurrentArea, bool forceSelected, RenderContext context)
    {
        var flags = forceSelected ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (!ImGui.BeginTabItem(label, flags))
        {
            return;
        }

        selectedTerritory = territoryId;

        if (isCurrentArea)
        {
            Plugin.Modules.RenderMainUi(context);
        }
        else
        {
            ImGui.TextUnformatted("このタブの機能は、該当エリアにいる間に利用できます。");
        }

        ImGui.EndTabItem();
    }
}
