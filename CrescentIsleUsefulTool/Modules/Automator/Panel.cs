using System;
using System.Collections.Generic;
using System.Linq;
using CrescentIsleUsefulTool.Data;
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
                CrescentTheme.Status(
                    "稼働ステータス",
                    module.IsEnabled ? "ON" : "OFF",
                    module.IsEnabled ? CrescentTheme.Success : CrescentTheme.Muted
                );

                ImGui.Spacing();
                ImGui.TextDisabled("実行状態");
                ImGui.TextWrapped(module.automator.RuntimeStatus);

                ImGui.Spacing();
                ImGui.TextDisabled(module.T("panel.activity.label"));
            try
            {
                var name = module.automator.Activity?.GetName() ?? module.T("panel.activity.none");
                ImGui.TextUnformatted(name);
            }
            catch (AccessViolationException)
            {
                    CrescentTheme.EmptyState(module.T("panel.activity.none"));
            }

                ImGui.Spacing();
                ImGui.TextDisabled(module.T("panel.activity_state.label"));
                ImGui.TextColored(
                    module.automator.Activity == null ? CrescentTheme.Muted : CrescentTheme.AccentSoft,
                    module.automator.Activity?.state.ToLabel() ?? module.T("panel.activity_state.none")
                );
            },
            "マジックポット → CE → FATE の優先順で移動します。",
            module.IsEnabled ? CrescentTheme.Success : CrescentTheme.Muted
        );
    }

    /// <summary>
    /// North Horn content belongs on the automation-mode configuration page,
    /// not on the main status page.  The generated South Horn settings remain
    /// above this compact two-tab catalogue.
    /// </summary>
    public void DrawConfigurationCatalog(AutomatorModule module)
    {
        ImGui.TextColored(CrescentTheme.AccentSoft, "北征編 自動操作対象");
        ImGui.TextDisabled("自動移動の対象を選択できます。チェックを外した項目は無視されます。");
        ImGui.Spacing();

        if (!ImGui.BeginTabBar("##NorthHornAutomationModeCatalog"))
        {
            return;
        }

        var changed = false;
        if (ImGui.BeginTabItem("CE（15件）"))
        {
            changed |= DrawBulkControls("NorthCE", NorthHornContent.CriticalEncounters, module.Config.NorthCriticalEncounters);
            changed |= DrawActivityTable(
                "NorthCriticalEncounters",
                NorthHornContent.CriticalEncounters,
                module.Config.NorthCriticalEncounters,
                module.Config.IsNorthCriticalEncounterEnabled
            );
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("FATE（13件）"))
        {
            changed |= DrawBulkControls("NorthFATE", NorthHornContent.Fates, module.Config.NorthFates);
            changed |= DrawActivityTable(
                "NorthFates",
                NorthHornContent.Fates,
                module.Config.NorthFates,
                module.Config.IsNorthFateEnabled
            );
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
        IReadOnlyList<NorthHornContent.ActivityInfo> activities,
        IDictionary<uint, bool> settings,
        Func<uint, bool> isEnabled)
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
                settings[activity.Id] = enabled;
                changed = true;
            }

            ImGui.SameLine();
            if (activity.IsMagicPot)
            {
                ImGui.TextColored(CrescentTheme.Warning, "[MAGIC POT]");
                ImGui.SameLine();
            }

            ImGui.TextWrapped(activity.JapaneseName);
            ImGui.TextDisabled(activity.EnglishName);
            ImGui.TextColored(CrescentTheme.Muted, $"{activity.Location}   ID:{activity.Id}");
        }

        ImGui.EndTable();
        return changed;
    }

    private static bool DrawBulkControls(
        string id,
        IReadOnlyList<NorthHornContent.ActivityInfo> activities,
        IDictionary<uint, bool> settings)
    {
        var changed = false;
        ImGui.TextDisabled($"有効 {activities.Count(activity => !settings.TryGetValue(activity.Id, out var enabled) || enabled)} / {activities.Count}");
        ImGui.SameLine();
        if (ImGui.SmallButton($"すべて有効##{id}"))
        {
            foreach (var activity in activities)
            {
                settings[activity.Id] = true;
            }

            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"すべて無効##{id}"))
        {
            foreach (var activity in activities)
            {
                settings[activity.Id] = false;
            }

            changed = true;
        }

        ImGui.Spacing();
        return changed;
    }
}
