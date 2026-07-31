using System.Linq;
using System.Numerics;
using BOCCHI.Modules;
using BOCCHI.Modules.Automator;
using BOCCHI.Ui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Ocelot.Modules;
using Ocelot.Windows;

namespace BOCCHI.Windows;

[OcelotConfigWindow]
public class ConfigWindow(Plugin primaryPlugin, Config config) : OcelotConfigWindow(primaryPlugin, config)
{
    private IModule? selectedConfigModule;
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
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 540),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    protected override void Render(RenderContext context)
    {
        using var theme = CrescentTheme.Push();
        var modules = Plugin.Modules.GetModulesByConfigOrder().ToList();
        selectedConfigModule ??= modules.FirstOrDefault();

        DrawHeader();
        ImGui.Spacing();

        var navigationWidth = ImGui.GetContentRegionAvail().X >= 900f ? 280f : 235f;
        using (ImRaii.Child("##LeftPanel", new Vector2(navigationWidth, 0), true))
        {
            ImGui.TextColored(CrescentTheme.AccentSoft, "機能設定");
            ImGui.TextDisabled("設定する項目を選択してください");
            ImGui.Separator();
            ImGui.Spacing();

            foreach (var module in modules)
            {
                if (module is not Module concreteModule || concreteModule.Config == null)
                {
                    continue;
                }

                var name = concreteModule.Config.GetType().Name.Replace("Config", string.Empty);

                var title = concreteModule.Config.GetTitle();
                if (title != null)
                {
                    name = title;
                }

                var selected = module == selectedConfigModule;
                if (ImGui.Selectable($"  {name}##ConfigModule", selected, ImGuiSelectableFlags.None, new Vector2(0f, 32f)))
                {
                    selectedConfigModule = module;
                }
            }
        }

        ImGui.SameLine();

        var activeModule = selectedConfigModule;
        if (activeModule == null)
        {
            ImGui.TextDisabled("No configurable modules are available.");
            return;
        }

        using (ImRaii.Child("##RightPanel", new Vector2(0, 0), true))
        {
            var settingsTitle = activeModule is Module selectedModule
                ? selectedModule.Config?.GetTitle()
                : null;
            CrescentTheme.Card(
                "ActiveConfiguration",
                settingsTitle ?? "SETTINGS",
                () => DrawConfigurationBody(activeModule, context),
                "変更内容は設定ファイルへ保存されます。"
            );
        }
    }

    private static void DrawConfigurationBody(IModule activeModule, RenderContext context)
    {
        if (activeModule is not AutomatorModule automator)
        {
            activeModule.RenderConfigUi(context);
            return;
        }

        if (!ImGui.BeginTabBar("##IllegalModeSettingsAreas"))
        {
            return;
        }

        if (ImGui.BeginTabItem("基本・南征編"))
        {
            ImGui.TextDisabled("不正モードの共通動作と南征編の対象を設定します。");
            ImGui.Spacing();
            activeModule.RenderConfigUi(context);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("北征編"))
        {
            automator.panel.DrawConfigurationCatalog(automator);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private static void DrawHeader()
    {
        if (!ImGui.BeginTable("##ConfigHeader", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
        {
            return;
        }

        ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Context", ImGuiTableColumnFlags.WidthFixed, 170f);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.AccentSoft, "CRESCENT ISLE SETTINGS");
        ImGui.TextDisabled("機能・通知・自動化の設定");
        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.Accent, "● CONFIGURATION");
        ImGui.TextDisabled("Blue Horizon Theme");
        ImGui.EndTable();
    }
}
