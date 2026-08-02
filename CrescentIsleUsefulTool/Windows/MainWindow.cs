using System;
using System.Collections.Generic;
using System.Linq;
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
using CrescentIsleUsefulTool.Modules.Teleporter;
using CrescentIsleUsefulTool.Modules.Treasure;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Ocelot;
using Ocelot.Windows;
using CiutPlugin = CrescentIsleUsefulTool.Plugin;

namespace CrescentIsleUsefulTool.Windows;

[OcelotMainWindow]
public class MainWindow(Plugin primaryPlugin, Config config) : OcelotMainWindow(primaryPlugin, config)
{
    private uint selectedTerritory;
    private uint previousTerritory;
    private bool windowThemePushed;

    protected override string GetWindowName()
    {
        return $"Crescent Isle Useful Tool v{CiutPlugin.DisplayVersion}##Main";
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
        ImGui.TextDisabled("統合探索ダッシュボード");

        ImGui.TableNextColumn();
        ImGui.TextColored(CrescentTheme.Accent, currentTerritory == ZoneData.NORTHHORN ? "● 北征編" : "● 南征編");

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
        DrawPage("優先順位", DrawPriorityPage);
        DrawPage("宝箱・探索", () => DrawTreasurePage(context));
        DrawPage("支援ツール", () => DrawUtilityPage(context));
        DrawPage("計測", () => DrawMetricsPage(context));
        DrawPage("現在の設定", DrawCurrentSettingsPage);
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
        var carrots = Plugin.Modules.GetModule<CarrotsModule>();
        if (!carrots.IsEnabled)
        {
            RenderModule<TreasureModule>(context);
            return;
        }

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

    private void DrawPriorityPage()
    {
        var automator = Plugin.Modules.GetModule<AutomatorModule>();
        CrescentTheme.Card(
            "AutomationPriorityOrder",
            "自動操作の優先順位",
            () =>
            {
                ImGui.TextWrapped("発生中の対象が複数ある場合、上にある種類から選択します。矢印で任意の順番に変更できます。");
                ImGui.TextDisabled("マジックポット予想が5分未満の待機処理と、参加中コンテンツ、完了後のデミデジョン帰還は安全上この順序より優先されます。");
                ImGui.Spacing();

                var order = automator.Config.GetPriorityOrder().ToList();
                if (ImGui.BeginTable("##AutomationPriorityTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("順位", ImGuiTableColumnFlags.WidthFixed, 58f);
                    ImGui.TableSetupColumn("対象", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("変更", ImGuiTableColumnFlags.WidthFixed, 155f);
                    ImGui.TableHeadersRow();

                    for (var index = 0; index < order.Count; index++)
                    {
                        var priority = order[index];
                        ImGui.TableNextColumn();
                        ImGui.TextColored(index == 0 ? CrescentTheme.Accent : CrescentTheme.AccentSoft, $"{index + 1} 位");
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(priority.ToJapaneseLabel());
                        ImGui.TableNextColumn();

                        ImGui.BeginDisabled(index == 0);
                        if (ImGui.SmallButton($"↑ 上へ##PriorityUp_{priority}"))
                        {
                            automator.Config.MovePriority(priority, -1);
                            automator.PluginConfig.Save();
                        }
                        ImGui.EndDisabled();
                        ImGui.SameLine();
                        ImGui.BeginDisabled(index == order.Count - 1);
                        if (ImGui.SmallButton($"↓ 下へ##PriorityDown_{priority}"))
                        {
                            automator.Config.MovePriority(priority, 1);
                            automator.PluginConfig.Save();
                        }
                        ImGui.EndDisabled();
                    }

                    ImGui.EndTable();
                }
            },
            "変更内容は自動保存され、次の対象選択から反映されます。",
            CrescentTheme.AccentSoft);
    }

    private void DrawCurrentSettingsPage()
    {
        var automator = primaryPlugin.Config.AutomatorConfig;
        var treasure = primaryPlugin.Config.TreasureConfig;
        var magicPot = primaryPlugin.Config.MagicPotConfig;
        var buff = primaryPlugin.Config.BuffConfig;
        var fates = primaryPlugin.Config.FatesConfig;
        var criticalEncounters = primaryPlugin.Config.CriticalEncountersConfig;
        var carrots = primaryPlugin.Config.CarrotsConfig;
        var forkedTower = primaryPlugin.Config.ForkedTowerConfig;
        var mobFarmer = primaryPlugin.Config.MobFarmerConfig;
        var windowManager = primaryPlugin.Config.WindowManagerConfig;
        var eventDrop = primaryPlugin.Config.EventDropConfig;
        var pathfinder = primaryPlugin.Config.PathfinderConfig;
        var buffModule = Plugin.Modules.GetModule<BuffModule>();
        var lowestBuffSeconds = buffModule.BuffManager.GetLowestBuffTimer(buffModule);
        var buffRemaining = lowestBuffSeconds == int.MaxValue
            ? "監視無効"
            : lowestBuffSeconds <= 0
                ? "未付与"
                : $"最短 {lowestBuffSeconds / 60}分{lowestBuffSeconds % 60}秒";

        DrawSettingsCard("CurrentAutomationSettings", "自動操作", [
            Setting("自動操作モード", automator.Enabled),
            Setting("CEへの自動移動", automator.DoCriticalEncounters),
            Setting("FATEへの自動移動", automator.DoFates),
            Setting("戦闘AIの自動切り替え", automator.ToggleAiProvider),
            Setting("敵の自動ターゲット", automator.ForceTarget),
            Setting("敵集団の中央を優先", automator.ForceTargetCentralEnemy && automator.ForceTarget),
            Setting("CE移動前の待機", automator.DelayCriticalEncounters && automator.DoCriticalEncounters),
            new SettingItem("戦闘AI", automator.AiProvider.ToLabel(), CrescentTheme.AccentSoft),
            new SettingItem("交戦開始距離", $"{automator.EngagementRange:F1}m", CrescentTheme.AccentSoft),
            new SettingItem("南征編CE／FATE", $"{automator.CriticalEncountersMap.Count(item => item.Value)}／{automator.FatesMap.Count(item => item.Value)}件 有効", CrescentTheme.AccentSoft),
            new SettingItem("北征編CE／FATE", $"{NorthHornContent.CriticalEncounters.Count(item => automator.IsNorthCriticalEncounterEnabled(item.Id))}／{NorthHornContent.Fates.Count(item => automator.IsNorthFateEnabled(item.Id))}件 有効", CrescentTheme.AccentSoft),
            new SettingItem("優先順位", string.Join(" → ", automator.GetPriorityOrder().Select(item => item.ToJapaneseLabel())), CrescentTheme.AccentSoft),
        ]);

        DrawResponsiveColumns(
            () => DrawSettingsCard("CurrentTravelSettings", "移動・帰還", [
                new SettingItem("FATE・CE完了時", "デミデジョンで必ず帰還", CrescentTheme.Success),
                new SettingItem("帰還後", "エーテライト付近へ移動", CrescentTheme.AccentSoft),
                new SettingItem("イベントへの移動", "最寄りエーテライト経由", CrescentTheme.AccentSoft),
                new SettingItem("長距離移動", "自動マウント", CrescentTheme.AccentSoft),
                new SettingItem("探索対象の判定距離", $"{pathfinder.DetectionRange:F0}m", CrescentTheme.AccentSoft),
                new SettingItem("帰還経路コスト", $"{pathfinder.ReturnCost:F0}", CrescentTheme.AccentSoft),
                new SettingItem("テレポート経路コスト", $"{pathfinder.TeleportCost:F0}", CrescentTheme.AccentSoft),
            ]),
            () => DrawSettingsCard("CurrentTreasureSettings", "宝箱・マジックポット", [
                Setting("宝箱表示", treasure.Enabled),
                Setting("青銅宝箱へのライン", treasure.DrawLineToBronzeChests && treasure.Enabled),
                Setting("白銀宝箱へのライン", treasure.DrawLineToSilverChests && treasure.Enabled),
                Setting("トレジャーハンター", treasure.EnableTreasureHunt && treasure.Enabled),
                Setting("帰還時のマギ・トレジャーサーチ", treasure.CastTreasureSightUponReturn),
                Setting("宝箱割合を表示", treasure.ShowPercentageActiveTreasureCount),
                Setting("マジックポット予想", magicPot.Enabled),
                Setting("マジックポット宝箱自動探索", magicPot.EnableTreasureSearchMode && magicPot.Enabled),
            ]));

        DrawResponsiveColumns(
            () => DrawSettingsCard("CurrentMonitorSettings", "FATE・CE監視", [
                Setting("FATE監視", fates.Enabled),
                Setting("FATE発生をログ出力", fates.LogSpawn && fates.Enabled),
                new SettingItem("FATE報酬通知", GetFateAlertSummary(fates), CrescentTheme.AccentSoft),
                Setting("CE監視", criticalEncounters.Enabled),
                Setting("CE発生をログ出力", criticalEncounters.LogSpawn && criticalEncounters.Enabled),
                Setting("フォークタワーを追跡", criticalEncounters.TrackForkedTower && criticalEncounters.Enabled),
                new SettingItem("CE報酬通知", GetCriticalEncounterAlertSummary(criticalEncounters), CrescentTheme.AccentSoft),
            ]),
            () => DrawSettingsCard("CurrentSupportSettings", "探索・計測・補助", [
                Setting("にんじん探索", carrots.Enabled),
                Setting("にんじんへのライン", carrots.DrawLineToCarrots && carrots.Enabled),
                Setting("にんじん自動探索", carrots.EnableCarrotHunt && carrots.Enabled),
                Setting("フォークタワー支援", forkedTower.Enabled),
                Setting("モブ討伐支援", mobFarmer.Enabled),
                new SettingItem("通貨・経験値計測", "常時有効", CrescentTheme.Success),
                new SettingItem("使用マウント", primaryPlugin.Config.MountConfig.MountRoulette ? "マウントルーレット" : $"マウントID {primaryPlugin.Config.MountConfig.Mount}", CrescentTheme.AccentSoft),
            ]));

        DrawResponsiveColumns(
            () => DrawSettingsCard("CurrentBuffSettings", "たんきゅうしん・バフ", [
                Setting("バフ機能", buff.Enabled),
                Setting("たんきゅうしんを使用", buff.UseInquiringMind && buff.Enabled),
                new SettingItem("再適用しきい値", $"残り{buff.ReapplyThreshold}分", CrescentTheme.AccentSoft),
                new SettingItem(
                    "現在の最短残り時間",
                    buffRemaining,
                    lowestBuffSeconds == int.MaxValue
                        ? CrescentTheme.Muted
                        : lowestBuffSeconds <= buff.ReapplyThreshold * 60
                            ? CrescentTheme.Warning
                            : CrescentTheme.Success),
            ]),
            () => DrawSettingsCard("CurrentDisplaySettings", "表示・ウィンドウ", [
                Setting("デミアートマ報酬表示", eventDrop.ShowDemiatmaDrops),
                Setting("ノート報酬表示", eventDrop.ShowNoteDrops),
                Setting("ソウルシャード報酬表示", eventDrop.ShowSoulShardDrops),
                Setting("起動時にメイン画面を開く", windowManager.OpenMainOnStartUp),
                Setting("エリア入場時にメイン画面を開く", windowManager.OpenMainOnEnter),
                Setting("エリア退出時にメイン画面を閉じる", windowManager.CloseMainOnExit),
                Setting("戦闘中にメイン画面を隠す", windowManager.HideMainInCombat),
            ]));
    }

    private static void DrawSettingsCard(string id, string title, IReadOnlyCollection<SettingItem> settings)
    {
        CrescentTheme.Card(id, title, () =>
        {
            if (!ImGui.BeginTable($"##{id}Table", 2, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                return;
            }

            ImGui.TableSetupColumn("設定", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("現在値", ImGuiTableColumnFlags.WidthFixed, 230f);
            foreach (var setting in settings)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(setting.Label);
                ImGui.TableNextColumn();
                ImGui.TextColored(setting.Color, setting.Value);
            }

            ImGui.EndTable();
        }, "現在保存されている設定を表示します。");
    }

    private static SettingItem Setting(string label, bool enabled)
    {
        return new SettingItem(label, enabled ? "有効" : "無効", enabled ? CrescentTheme.Success : CrescentTheme.Muted);
    }

    private static string GetFateAlertSummary(FatesConfig config)
    {
        if (!config.Enabled)
        {
            return "監視無効";
        }

        if (config.AlertAll)
        {
            return "すべて通知";
        }

        var count = new[]
        {
            config.AlertAzurite,
            config.AlertVerdigris,
            config.AlertMalachite,
            config.AlertRealgar,
            config.AlertCaputMortuum,
            config.AlertOrpiment,
        }.Count(enabled => enabled);
        return count == 0 ? "通知なし" : $"個別 {count}種類";
    }

    private static string GetCriticalEncounterAlertSummary(CriticalEncountersConfig config)
    {
        if (!config.Enabled)
        {
            return "監視無効";
        }

        if (config.AlertAll)
        {
            return "すべて通知";
        }

        var count = new[]
        {
            config.AlertAzurite,
            config.AlertVerdigris,
            config.AlertMalachite,
            config.AlertRealgar,
            config.AlertCaputMortuum,
            config.AlertOrpiment,
            config.AlertOracle,
            config.AlertBerserker,
            config.AlertRanger,
        }.Count(enabled => enabled);
        return count == 0 ? "通知なし" : $"個別 {count}種類";
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
            CrescentTheme.EmptyState($"{GetModuleJapaneseName(typeof(T))}は設定で無効です。");
        }
    }

    private static string GetModuleJapaneseName(Type moduleType)
    {
        if (moduleType == typeof(CarrotsModule)) return "にんじん探索";
        if (moduleType == typeof(TreasureModule)) return "宝箱探索";
        if (moduleType == typeof(MagicPotModule)) return "マジックポット";
        if (moduleType == typeof(FatesModule)) return "FATE監視";
        if (moduleType == typeof(CriticalEncountersModule)) return "CE監視";
        if (moduleType == typeof(BuffModule)) return "バフ支援";
        if (moduleType == typeof(ForkedTowerModule)) return "フォークタワー支援";
        if (moduleType == typeof(MobFarmerModule)) return "モブ討伐支援";
        if (moduleType == typeof(CurrencyModule)) return "通貨計測";
        if (moduleType == typeof(ExpModule)) return "経験値計測";
        return "この機能";
    }

    private readonly record struct SettingItem(string Label, string Value, Vector4 Color);
}
