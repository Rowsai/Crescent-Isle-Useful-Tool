using Dalamud.Interface;
using CrescentIsleUsefulTool.Ui;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;

namespace CrescentIsleUsefulTool.Modules.Currency;

public class Panel
{
    public void Draw(CurrencyModule module)
    {
        CrescentTheme.Card("Currency", module.T("panel.title"), () =>
        {
            if (ImGui.BeginTable("CurrencyData##CrescentIsleUsefulTool", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Reset", ImGuiTableColumnFlags.WidthFixed, 42f);
                ImGui.TableSetupColumn("Currency", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("PerHour", ImGuiTableColumnFlags.WidthFixed, 110f);
                // Silver
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (ImGuiEx.IconButton(FontAwesomeIcon.Redo, "Reset##Silver"))
                {
                    module.Tracker.ResetSilver();
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(module.T("panel.silver.label"));

                ImGui.TableNextColumn();
                ImGui.TextColored(CrescentTheme.AccentSoft, module.Tracker.GetSilverPerHour().ToString("F2"));

                // Gold
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (ImGuiEx.IconButton(FontAwesomeIcon.Redo, "Reset##Gold"))
                {
                    module.Tracker.ResetGold();
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(module.T("panel.gold.label"));

                ImGui.TableNextColumn();
                ImGui.TextColored(CrescentTheme.Warning, module.Tracker.GetGoldPerHour().ToString("F2"));

                ImGui.EndTable();
            }
        }, "直近1時間あたりの獲得量");
    }
}
