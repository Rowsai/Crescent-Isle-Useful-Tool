using BOCCHI.Ui;
using Dalamud.Bindings.ImGui;

namespace BOCCHI.Modules.StateManager;

public class Panel
{
    public bool Draw(StateManagerModule module)
    {
        if (!module.Config.ShowDebug)
        {
            return false;
        }

        CrescentTheme.Card("StateManager", module.T("panel.title"), () =>
        {
            ImGui.TextDisabled(module.T("panel.state.label"));
            ImGui.SameLine();
            ImGui.TextColored(CrescentTheme.AccentSoft, module.GetStateText());
        }, "現在のキャラクター状態（デバッグ表示）");

        return true;
    }
}
