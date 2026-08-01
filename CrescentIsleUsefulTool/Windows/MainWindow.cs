using System;
using System.Numerics;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Modules;
using CrescentIsleUsefulTool.Modules.Automator;
using CrescentIsleUsefulTool.Modules.Buff;
using CrescentIsleUsefulTool.Modules.Carrots;
using CrescentIsleUsefulTool.Modules.CriticalEncounters;
using CrescentIsleUsefulTool.Modules.Currency;
using CrescentIsleUsefulTool.Modules.Exp;
using CrescentIsleUsefulTool.Modules.Fates;
using CrescentIsleUsefulTool.Modules.ForkedTower;
using CrescentIsleUsefulTool.Modules.MagicPot;
using CrescentIsleUsefulTool.Modules.MobFarmer;
using CrescentIsleUsefulTool.Modules.StateManager;
using CrescentIsleUsefulTool.Modules.Treasure;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Ocelot;
using Ocelot.Windows;

namespace CrescentIsleUsefulTool.Windows;

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
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620f, 420f),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        TitleBarButtons.Add(new TitleBarButton
        {
            Click = mouseButton =>
            {
                if (mouseButton == ImGuiMouseButton.Left)
                {
                    Plugin.Modules.GetModule<AutomatorModule>().DisableAutomationMode();
                }
            },
            Icon = FontAwesomeIcon.Stop,
            IconColor = CrescentTheme.Danger,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(I18N.T("windows.main.buttons.emergency_stop")),
        });

        TitleBarButtons.Add(new TitleBarButton
        {
            Click = mouseButton =>
            {
                if (mouseButton == ImGuiMouseButton.Left)
                {
                    AutomatorModule.ToggleAutomationMode(Plugin);
                }
            },
            Icon = FontAwesomeIcon.Robot,
            IconColor = CrescentTheme.AccentSoft,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(I18N.T("windows.main.buttons.toggle_automation_mode")),
        });
    }

    protected override void Render(RenderContext context)
    {
        using var theme = CrescentTheme.Push();

        if (!ZoneData.IsInOccultCrescent())
        {
            CrescentTheme.EmptyState(I18N.T("generic.label.not_in_zone"));
            return;
        }

        var currentTerritory = Svc.ClientState.TerritoryType;
        var selectCurrentArea = selectedTerritory == 0 || previousTerritory != currentTerritory;
        if (selectCurrentArea)
        {
            selectedTerritory = currentTerritory;
        }

        DrawCompactHeader(currentTerritory);

        if (ImGui.BeginTabBar("##OccultCrescentArea", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            DrawAreaTab("南征編", ZoneData.SOUTHHORN, ZoneData.IsInSouthHorn(), selectCurrentArea && currentTerritory == ZoneData.SOUTHHORN, context);
            DrawAreaTab("北征編", ZoneData.NORTHHORN, ZoneData.IsInNorthHorn(), selectCurrentArea && currentTerritory == ZoneData.NORTHHORN, context);
            ImGui.EndTabBar();
        }

        previousTerritory = currentTerritory;
    }

    private void DrawCompactHeader(uint currentTerritory)
    {
        var automator = Plugin.Modules.GetModule<AutomatorModule>();
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX;
        if (!ImGui.BeginTable("##CompactDashboardHeader", 3, flags))
        {
            return;
        }

        ImGui.TableSetupColumn("Identity", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Area", ImGuiTableColumnFlags.WidthFixed, 130f);
        ImGui.TableSetupColumn("Automation", ImGuiTableColumnFlags.WidthFixed, 170f);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.AccentSoft, "CIUT");
        ImGui.SameLine();
        ImGui.TextDisabled("OPERATIONS DASHBOARD");

        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.Accent, currentTerritory == ZoneData.NORTHHORN ? "● NORTH HORN" : "● SOUTH HORN");

        ImGui.TableNextColumn();
        var buttonLabel = automator.IsEnabled ? "自動操作を停止" : "自動操作を開始";
        if (ImGui.Button($"{buttonLabel}##HeaderAutomation", new Vector2(-1f, 0f)))
        {
            AutomatorModule.ToggleAutomationMode(Plugin);
        }

        ImGui.EndTable();
        ImGui.Spacing();
    }

    private void DrawAreaTab(string label, uint territoryId, bool isCurrentArea, bool forceSelected, RenderContext context)
    {
        var flags = forceSelected ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (!ImGui.BeginTabItem(label, flags))
        {
            return;
        }

        selectedTerritory = territoryId;
        if (!isCurrentArea)
        {
            CrescentTheme.EmptyState($"{label}へ移動すると監視情報と操作機能を表示します。");
            ImGui.EndTabItem();
            return;
        }

        DrawDashboardPages(context);
        ImGui.EndTabItem();
    }

    private void DrawDashboardPages(RenderContext context)
    {
        if (!ImGui.BeginTabBar("##CompactDashboardPages", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            return;
        }

        DrawPage("概要", () => DrawOverview(context));
        DrawPage("FATE・CE", () => DrawActivityPage(context));
        DrawPage("宝箱・探索", () => DrawTreasurePage(context));
        DrawPage("支援ツール", () => DrawUtilityPage(context));
        DrawPage("計測", () => DrawMetricsPage(context));
        ImGui.EndTabBar();
    }

    private static void DrawPage(string label, Action content)
    {
        if (!ImGui.BeginTabItem(label))
        {
            return;
        }

        ImGui.Spacing();
        content();
        ImGui.EndTabItem();
    }

    private void DrawOverview(RenderContext context)
    {
        RenderModule<AutomatorModule>(context);
        DrawResponsiveColumns(
            () => RenderModule<StateManagerModule>(context),
            () => RenderModule<MagicPotModule>(context));
    }

    private void DrawActivityPage(RenderContext context)
    {
        DrawResponsiveColumns(
            () => RenderModule<FatesModule>(context),
            () => RenderModule<CriticalEncountersModule>(context));
    }

    private void DrawTreasurePage(RenderContext context)
    {
        DrawResponsiveColumns(
            () => RenderModule<TreasureModule>(context),
            () => RenderModule<CarrotsModule>(context));
    }

    private void DrawUtilityPage(RenderContext context)
    {
        DrawResponsiveColumns(
            () =>
            {
                RenderModule<BuffModule>(context);
                RenderModule<ForkedTowerModule>(context);
            },
            () => RenderModule<MobFarmerModule>(context));
    }

    private void DrawMetricsPage(RenderContext context)
    {
        DrawResponsiveColumns(
            () => RenderModule<CurrencyModule>(context),
            () => RenderModule<ExpModule>(context));
    }

    private void DrawResponsiveColumns(Action left, Action right)
    {
        if (ImGui.GetContentRegionAvail().X < 820f)
        {
            left();
            right();
            return;
        }

        if (!ImGui.BeginTable("##ResponsiveDashboardColumns", 2, ImGuiTableFlags.SizingStretchSame))
        {
            return;
        }

        ImGui.TableNextColumn();
        left();
        ImGui.TableNextColumn();
        right();
        ImGui.EndTable();
    }

    private void RenderModule<T>(RenderContext context) where T : Module
    {
        var module = Plugin.Modules.GetModule<T>();
        // Its ON control belongs to this panel, so automation remains visible
        // while the mode itself is stopped.
        if (module.IsEnabled || module is AutomatorModule)
        {
            module.RenderMainUi(context);
        }
        else
        {
            CrescentTheme.EmptyState($"{typeof(T).Name.Replace("Module", string.Empty)} は設定で無効です。");
        }
    }
}
