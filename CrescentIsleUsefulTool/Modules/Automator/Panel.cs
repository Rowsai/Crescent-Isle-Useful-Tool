using System;
using System.Collections.Generic;
using System.Linq;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Modules.MagicPot;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;

namespace CrescentIsleUsefulTool.Modules.Automator;

public class Panel
{
    public void Draw(AutomatorModule module)
    {
        CrescentTheme.Card(
            "AutomationModeStatus",
            module.T("panel.title"),
            () =>
            {
                DrawQuickControls(module);
                ImGui.Spacing();
                if (ImGui.BeginTable("##AutomationSummary", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
                {
                    ImGui.TableNextColumn();
                    CrescentTheme.Status("ステータス", module.IsEnabled ? "有効" : "無効", module.IsEnabled ? CrescentTheme.Success : CrescentTheme.Muted);
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled("操作モード");
                    ImGui.TextColored(module.IsEnabled ? CrescentTheme.AccentSoft : CrescentTheme.Muted, GetActiveMode(module));
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(module.T("panel.activity_state.label"));
                    ImGui.TextColored(
                        module.automator.Activity == null ? CrescentTheme.Muted : CrescentTheme.AccentSoft,
                        module.automator.Activity?.state.ToLabel() ?? module.T("panel.activity_state.none"));
                    ImGui.EndTable();
                }

                ImGui.TextDisabled("現在の処理");
                ImGui.SameLine();
                ImGui.TextWrapped(module.automator.RuntimeStatus);
            },
            $"優先順位：{GetPrioritySummary(module)}",
            module.IsEnabled ? CrescentTheme.Success : CrescentTheme.Muted
        );
    }

    private static void DrawQuickControls(AutomatorModule module)
    {
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame;
        if (!ImGui.BeginTable("##MainActivityTravelControls", 3, flags))
        {
            return;
        }

        ImGui.TableNextColumn();
        if (ImGui.Button(module.IsEnabled ? "自動操作を停止##MainAutomationToggle" : "自動操作を開始##MainAutomationToggle", new System.Numerics.Vector2(-1f, 0f)))
        {
            if (module.IsEnabled)
            {
                module.DisableAutomationMode();
            }
            else
            {
                module.EnableAutomationMode();
            }
        }

        ImGui.TableNextColumn();
        var ceEnabled = module.Config.DoCriticalEncounters;
        if (ImGui.Button(
                ceEnabled ? "CE移動を停止##MainCeToggle" : "CE移動を開始##MainCeToggle",
                new System.Numerics.Vector2(-1f, 0f)))
        {
            module.SetCriticalEncounterTravelEnabled(!ceEnabled);
            ceEnabled = !ceEnabled;
        }

        ImGui.TableNextColumn();
        var fateEnabled = module.Config.DoFates;
        if (ImGui.Button(
                fateEnabled ? "FATE移動を停止##MainFateToggle" : "FATE移動を開始##MainFateToggle",
                new System.Numerics.Vector2(-1f, 0f)))
        {
            module.SetFateTravelEnabled(!fateEnabled);
            fateEnabled = !fateEnabled;
        }

        ImGui.EndTable();

        ImGui.TextColored(
            !ceEnabled && !fateEnabled ? CrescentTheme.Warning : CrescentTheme.Muted,
            !ceEnabled && !fateEnabled ? "CE・FATEともに無効：拠点待機" : $"優先順位：{GetPrioritySummary(module)}");
    }

    private static string GetPrioritySummary(AutomatorModule module)
    {
        return string.Join(" → ", module.Config.GetPriorityOrder().Select(priority => priority.ToJapaneseLabel()));
    }

    private static string GetActiveMode(AutomatorModule module)
    {
        if (!module.IsEnabled)
        {
            return "停止中";
        }

        if (module.TryGetModule<MagicPotModule>(out var magicPot) && magicPot?.IsTreasureSearchActive == true)
        {
            return "マジックポット宝箱探索";
        }

        var activity = module.automator.Activity;
        if (activity == null)
        {
            return "監視・拠点待機";
        }

        if (activity.data.IsPot || NorthHornContent.IsMagicPotFate(activity.data.Id))
        {
            return "マジックポットFATE";
        }

        return activity.data.Type == EventType.CriticalEncounter ? "CEへ移動・参加中" : "FATEへ移動・参加中";
    }

    public void DrawBasicConfiguration(AutomatorModule module)
    {
        var changed = false;
        CrescentTheme.Status("自動操作モード", module.IsEnabled ? "稼働中" : "停止中", module.IsEnabled ? CrescentTheme.Success : CrescentTheme.Muted);
        ImGui.TextDisabled("自動操作モード自体の開始・停止は、メイン画面のボタンから操作します。");
        ImGui.Spacing();

        ImGui.TextUnformatted("戦闘AIプロバイダー");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##AutomationAiProvider", module.Config.AiProvider.ToLabel()))
        {
            foreach (var provider in Enum.GetValues<AiType>())
            {
                if (ImGui.Selectable(provider.ToLabel(), provider == module.Config.AiProvider))
                {
                    module.Config.AiProvider = provider;
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }

        var toggleAi = module.Config.ToggleAiProvider;
        if (ImGui.Checkbox("FATE・CE参加時に戦闘AIを自動切り替え##ToggleAiProvider", ref toggleAi))
        {
            module.Config.ToggleAiProvider = toggleAi;
            changed = true;
        }

        var forceTarget = module.Config.ForceTarget;
        if (ImGui.Checkbox("参加中の敵を自動ターゲット##ForceTarget", ref forceTarget))
        {
            module.Config.ForceTarget = forceTarget;
            changed = true;
        }

        var centralTarget = module.Config.ForceTargetCentralEnemy;
        if (ImGui.Checkbox("敵集団の中央を優先##ForceCentralTarget", ref centralTarget))
        {
            module.Config.ForceTargetCentralEnemy = centralTarget;
            changed = true;
        }

        var delayCe = module.Config.DelayCriticalEncounters;
        if (ImGui.Checkbox("CEへ向かう前に10～15秒待機##DelayCriticalEncounter", ref delayCe))
        {
            module.Config.DelayCriticalEncounters = delayCe;
            changed = true;
        }

        var range = module.Config.EngagementRange;
        ImGui.TextUnformatted($"交戦開始距離：{range:F1}m");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderFloat("##AutomationEngagementRange", ref range, 5f, 30f, "%.1f m"))
        {
            module.Config.EngagementRange = range;
            changed = true;
        }

        if (changed)
        {
            module.PluginConfig.Save();
        }
    }

    public void DrawSouthConfiguration(AutomatorModule module)
    {
        var criticalEncounters = module.Config.CriticalEncountersMap.Keys
            .OrderBy(id => id)
            .Select(id => new ActivityOption(id, EventData.GetCriticalEncounterDisplayName(id), $"コンテンツID：{id}"))
            .ToList();
        var fates = module.Config.FatesMap.Keys
            .OrderBy(id => id)
            .Select(id => new ActivityOption(id, EventData.GetFateDisplayName(id), $"コンテンツID：{id}", id is 1976 or 1977))
            .ToList();

        DrawAreaCatalog(
            module,
            "南征編",
            "South",
            criticalEncounters,
            fates,
            id => module.Config.CriticalEncountersMap.TryGetValue(id, out var enabled) && enabled,
            module.Config.SetSouthCriticalEncounterEnabled,
            id => module.Config.FatesMap.TryGetValue(id, out var enabled) && enabled,
            module.Config.SetSouthFateEnabled);
    }

    public void DrawNorthConfiguration(AutomatorModule module)
    {
        var criticalEncounters = NorthHornContent.CriticalEncounters
            .Select(activity => new ActivityOption(activity.Id, activity.JapaneseName, activity.Location))
            .ToList();
        var fates = NorthHornContent.Fates
            .Select(activity => new ActivityOption(activity.Id, activity.JapaneseName, activity.Location, activity.IsMagicPot))
            .ToList();

        DrawAreaCatalog(
            module,
            "北征編",
            "North",
            criticalEncounters,
            fates,
            module.Config.IsNorthCriticalEncounterEnabled,
            (id, enabled) => module.Config.NorthCriticalEncounters[id] = enabled,
            module.Config.IsNorthFateEnabled,
            (id, enabled) => module.Config.NorthFates[id] = enabled);
    }

    private static void DrawAreaCatalog(
        AutomatorModule module,
        string areaName,
        string id,
        IReadOnlyList<ActivityOption> criticalEncounters,
        IReadOnlyList<ActivityOption> fates,
        Func<uint, bool> isCriticalEnabled,
        Action<uint, bool> setCriticalEnabled,
        Func<uint, bool> isFateEnabled,
        Action<uint, bool> setFateEnabled)
    {
        ImGui.TextColored(CrescentTheme.AccentSoft, $"{areaName} 自動操作対象");
        ImGui.TextDisabled("チェックを外したコンテンツは、自動移動の対象から除外します。");
        ImGui.Spacing();
        if (!ImGui.BeginTabBar($"##{id}AutomationCatalog", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            return;
        }

        var changed = false;
        if (ImGui.BeginTabItem($"CE（{criticalEncounters.Count}件）"))
        {
            changed |= DrawBulkControls($"{id}CE", criticalEncounters, isCriticalEnabled, setCriticalEnabled);
            changed |= DrawActivityTable($"{id}CriticalEncounters", criticalEncounters, isCriticalEnabled, setCriticalEnabled);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem($"FATE（{fates.Count}件）"))
        {
            changed |= DrawBulkControls($"{id}FATE", fates, isFateEnabled, setFateEnabled);
            changed |= DrawActivityTable($"{id}Fates", fates, isFateEnabled, setFateEnabled);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
        if (changed)
        {
            module.PluginConfig.Save();
        }
    }

    private static bool DrawActivityTable(
        string id,
        IReadOnlyList<ActivityOption> activities,
        Func<uint, bool> isEnabled,
        Action<uint, bool> setEnabled)
    {
        var columnCount = ImGui.GetContentRegionAvail().X >= 720f ? 2 : 1;
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame;
        if (!ImGui.BeginTable($"##{id}", columnCount, flags))
        {
            return false;
        }

        var changed = false;
        foreach (var activity in activities)
        {
            ImGui.TableNextColumn();
            var enabled = isEnabled(activity.Id);
            if (ImGui.Checkbox($"##{id}_{activity.Id}", ref enabled))
            {
                setEnabled(activity.Id, enabled);
                changed = true;
            }

            ImGui.SameLine();
            if (activity.IsMagicPot)
            {
                ImGui.TextColored(CrescentTheme.Warning, "［マジックポット］");
                ImGui.SameLine();
            }

            ImGui.TextWrapped(activity.Name);
            ImGui.TextColored(CrescentTheme.Muted, $"{activity.Detail}　ID：{activity.Id}");
        }

        ImGui.EndTable();
        return changed;
    }

    private static bool DrawBulkControls(
        string id,
        IReadOnlyList<ActivityOption> activities,
        Func<uint, bool> isEnabled,
        Action<uint, bool> setEnabled)
    {
        var changed = false;
        ImGui.TextDisabled($"有効 {activities.Count(activity => isEnabled(activity.Id))} / {activities.Count}");
        ImGui.SameLine();
        if (ImGui.SmallButton($"すべて有効##{id}"))
        {
            foreach (var activity in activities)
            {
                setEnabled(activity.Id, true);
            }

            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"すべて無効##{id}"))
        {
            foreach (var activity in activities)
            {
                setEnabled(activity.Id, false);
            }

            changed = true;
        }

        ImGui.Spacing();
        return changed;
    }

    private readonly record struct ActivityOption(uint Id, string Name, string Detail, bool IsMagicPot = false);
}
