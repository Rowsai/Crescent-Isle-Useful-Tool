using Dalamud.Interface;
using BOCCHI.Ui;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;

namespace BOCCHI.Modules.Exp;

public class Panel
{
    public void Draw(ExpModule module)
    {
        CrescentTheme.Card("Experience", module.T("panel.title"), () =>
        {
            if (ImGuiEx.IconButton(FontAwesomeIcon.Redo, $"Reset##Exp"))
            {
                module.tracker.Reset();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(module.T("panel.exp.label"));

            ImGui.SameLine();
            ImGui.TextColored(CrescentTheme.AccentSoft, module.tracker.GetExpPerHour().ToString("F2"));
        }, "直近1時間あたりの獲得経験値");
    }
}
