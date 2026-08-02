using CrescentIsleUsefulTool.Modules.Automator;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;

namespace CrescentIsleUsefulTool.Modules.StateManager;

public class Panel
{
    public bool Draw(StateManagerModule module)
    {
        CrescentTheme.Card("StateManager", module.T("panel.title"), () =>
        {
            ImGui.TextDisabled(module.T("panel.state.label"));
            ImGui.SameLine();
            ImGui.TextColored(CrescentTheme.AccentSoft, module.GetStateText());

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(CrescentTheme.AccentSoft, "次の実行ステップ");

            var automator = module.GetModule<AutomatorModule>();
            var steps = automator.automator.GetExecutionPlan(automator);
            if (ImGui.BeginTable("##AutomationExecutionPlan", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("No", ImGuiTableColumnFlags.WidthFixed, 32f);
                ImGui.TableSetupColumn("Step", ImGuiTableColumnFlags.WidthStretch);
                for (var index = 0; index < steps.Count; index++)
                {
                    ImGui.TableNextColumn();
                    ImGui.TextColored(index == 0 ? CrescentTheme.Accent : CrescentTheme.Muted, $"{index + 1}");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(steps[index]);
                }

                ImGui.EndTable();
            }
        }, "現在状態と、この後に実行する5ステップを表示します。", CrescentTheme.AccentSoft);

        return true;
    }
}
