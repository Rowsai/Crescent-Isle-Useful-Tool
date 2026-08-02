using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Modules;
using CrescentIsleUsefulTool.Modules.Automator;
using CrescentIsleUsefulTool.Modules.Buff;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Ocelot.Modules;
using Ocelot.Windows;
using CiutPlugin = CrescentIsleUsefulTool.Plugin;

namespace CrescentIsleUsefulTool.Windows;

[OcelotConfigWindow]
public class ConfigWindow(Plugin primaryPlugin, Config config) : OcelotConfigWindow(primaryPlugin, config)
{
    private static readonly string[] CategoryOrder = ["自動操作", "探索・コンテンツ", "移動・支援", "表示・計測"];

    private IModule? selectedConfigModule;
    private bool windowThemePushed;

    protected override string GetWindowName()
    {
        return $"Crescent Isle Useful Tool 設定 v{CiutPlugin.DisplayVersion}##Config";
    }

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
            MinimumSize = new Vector2(680f, 480f),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    protected override void Render(RenderContext context)
    {
        using var theme = CrescentTheme.Push();
        var modules = Plugin.Modules.GetModulesByConfigOrder()
            .OfType<Module>()
            .Where(module => module.Config != null)
            .Where(module => module.Config!.GetType().Name is not ("TeleporterConfig" or "CurrencyConfig" or "ExpConfig"))
            .ToList();
        selectedConfigModule ??= modules.FirstOrDefault();

        DrawCompactHeader(modules.Count);
        ImGui.Spacing();

        var navigationWidth = ImGui.GetContentRegionAvail().X >= 900f ? 230f : 195f;
        using (ImRaii.Child("##CiutConfigNavigation", new Vector2(navigationWidth, 0), true))
        {
            DrawNavigation(modules);
        }

        ImGui.SameLine();
        using (ImRaii.Child("##CiutConfigContent", new Vector2(0, 0), true))
        {
            if (selectedConfigModule is not Module activeModule)
            {
                CrescentTheme.EmptyState("設定項目を選択してください。");
                return;
            }

            DrawConfigurationHeader(activeModule);
            ImGui.Spacing();
            DrawConfigurationBody(activeModule, context);
        }
    }

    private void DrawNavigation(IReadOnlyCollection<Module> modules)
    {
        ImGui.TextColored(CrescentTheme.AccentSoft, "設定メニュー");
        ImGui.TextDisabled("カテゴリから機能を選択");
        ImGui.Spacing();

        foreach (var category in CategoryOrder)
        {
            var categoryModules = modules.Where(module => GetCategory(module) == category).ToList();
            if (categoryModules.Count == 0)
            {
                continue;
            }

            ImGui.TextColored(CrescentTheme.Muted, category);
            ImGui.Separator();
            foreach (var module in categoryModules)
            {
                var selected = ReferenceEquals(module, selectedConfigModule);
                var label = GetModuleTitle(module);
                if (ImGui.Selectable($"  {label}##ConfigModule_{module.GetType().Name}", selected, ImGuiSelectableFlags.None, new Vector2(0f, 30f)))
                {
                    selectedConfigModule = module;
                }
            }

            ImGui.Spacing();
        }
    }

    private static void DrawConfigurationHeader(Module module)
    {
        if (!ImGui.BeginTable("##ActiveConfigHeader", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
        {
            return;
        }

        ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed, 155f);
        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.AccentSoft, GetModuleTitle(module));
        ImGui.TextDisabled("変更内容は自動的に保存されます");
        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.Accent, $"● {GetCategory(module)}");
        ImGui.EndTable();
    }

    private static void DrawConfigurationBody(Module activeModule, RenderContext context)
    {
        if (activeModule is BuffModule buff)
        {
            DrawBuffConfiguration(buff);
            return;
        }

        if (activeModule is AutomatorModule automator)
        {
            DrawAutomationConfiguration(automator, context);
            return;
        }

        CrescentTheme.Card(
            $"Config_{activeModule.GetType().Name}",
            "機能設定",
            () =>
            {
                ImGui.PushItemWidth(MathF.Max(180f, ImGui.GetContentRegionAvail().X * 0.62f));
                activeModule.RenderConfigUi(context);
                ImGui.PopItemWidth();
            },
            "CIUT共通テーマで設定を表示しています。"
        );
    }

    private static void DrawAutomationConfiguration(AutomatorModule automator, RenderContext context)
    {
        if (!ImGui.BeginTabBar("##AutomationConfigurationPages", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            return;
        }

        if (ImGui.BeginTabItem("基本設定"))
        {
            CrescentTheme.Card(
                "AutomationCommonConfig",
                "自動操作の基本設定",
                () => automator.panel.DrawBasicConfiguration(automator),
                "開始時はナレッジクリスタル付近へ移動し、アクションID 46606「たんきゅうしん」を使用後、元のサポートジョブへ戻ります。"
            );
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("南征編"))
        {
            CrescentTheme.Card(
                "AutomationSouthConfig",
                "南征編の対象設定",
                () => automator.panel.DrawSouthConfiguration(automator),
                "CEとFATEを北征編と同じ一覧レイアウトで個別設定できます。"
            );
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("北征編"))
        {
            CrescentTheme.Card(
                "AutomationNorthConfig",
                "北征編の対象設定",
                () => automator.panel.DrawNorthConfiguration(automator),
                "マジックポット、CE、FATEの対象を整理して表示します。"
            );
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private static void DrawBuffConfiguration(BuffModule buff)
    {
        CrescentTheme.Card(
            "TankyushinConfig",
            "たんきゅうしん",
            () =>
            {
                CrescentTheme.Status("アクション", "たんきゅうしん / ID 46606", CrescentTheme.AccentSoft);
                ImGui.TextWrapped("ナレッジクリスタル付近へ移動後、すっぴんへ一時変更して4種類の30分バフをまとめて付与し、実行前のサポートジョブへ戻ります。");
                ImGui.Spacing();

                var enabled = buff.Config.Enabled;
                if (ImGui.Checkbox("バフ機能を有効にする##BuffEnabled", ref enabled))
                {
                    buff.Config.Enabled = enabled;
                    buff.PluginConfig.Save();
                }

                var useTankyushin = buff.Config.UseInquiringMind;
                if (ImGui.Checkbox("すっぴんの「たんきゅうしん」を使用##UseTankyushin", ref useTankyushin))
                {
                    buff.Config.UseInquiringMind = useTankyushin;
                    buff.PluginConfig.Save();
                }

                ImGui.TextDisabled("この設定は通常の残り時間監視に使用します。自動操作／トレジャーハンター開始時は、設定にかかわらず必ず1回実行します。");
                ImGui.Spacing();

                var threshold = buff.Config.ReapplyThreshold;
                ImGui.TextUnformatted("再適用しきい値");
                ImGui.SameLine();
                ImGui.TextColored(CrescentTheme.AccentSoft, $"残り {threshold}分");
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderInt("##TankyushinThreshold", ref threshold, 0, 25, "%d 分"))
                {
                    buff.Config.ReapplyThreshold = threshold;
                    buff.PluginConfig.Save();
                }

                ImGui.TextDisabled("対象バフが1つでも未付与、またはこの時間以下になると再使用します。");
            },
            "いのり・かまえる・あいのうた・クイックステップを一括管理",
            CrescentTheme.Success
        );

        CrescentTheme.Card(
            "TankyushinEffects",
            "付与対象",
            () =>
            {
                if (!ImGui.BeginTable("##TankyushinEffectsTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
                {
                    return;
                }

                DrawEffect("いのり", "防御支援");
                DrawEffect("かまえる", "移動・戦闘支援");
                DrawEffect("あいのうた", "継続支援");
                DrawEffect("クイックステップ", "行動支援");
                ImGui.EndTable();
            }
        );
    }

    private static void DrawEffect(string name, string description)
    {
        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.Success, $"● {name}");
        ImGui.TextDisabled(description);
    }

    private static void DrawCompactHeader(int moduleCount)
    {
        if (!ImGui.BeginTable("##ConfigWindowHeader", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
        {
            return;
        }

        ImGui.TableSetupColumn("Identity", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, 125f);
        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.AccentSoft, "CIUT 設定");
        ImGui.SameLine();
        ImGui.TextDisabled($"バージョン {CiutPlugin.DisplayVersion}");
        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.Accent, $"● 設定項目 {moduleCount}件");
        ImGui.EndTable();
    }

    private static string GetModuleTitle(Module module)
    {
        return module.Config?.GetTitle() ?? module.Config?.GetType().Name.Replace("Config", string.Empty) ?? module.GetType().Name.Replace("Module", string.Empty);
    }

    private static string GetCategory(Module module)
    {
        var configName = module.Config?.GetType().Name ?? string.Empty;
        return configName switch
        {
            "AutomatorConfig" or "BuffConfig" or "MobFarmerConfig" => "自動操作",
            "FatesConfig" or "CriticalEncountersConfig" or "MagicPotConfig" or "TreasureConfig" or "CarrotsConfig" or "ForkedTowerConfig" => "探索・コンテンツ",
            "MountConfig" or "TeleporterConfig" or "PathfinderConfig" => "移動・支援",
            _ => "表示・計測",
        };
    }
}
