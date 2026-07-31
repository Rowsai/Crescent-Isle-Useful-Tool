using BOCCHI.Data;
using BOCCHI.Ui;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace BOCCHI.Modules.ForkedTower;

public class Panel
{
    public void Draw(ForkedTowerModule module)
    {
        if (!ZoneData.IsInForkedTower())
        {
            return;
        }

        CrescentTheme.Card("ForkedTower", "FORKED TOWER", () =>
        {
            var state = OcelotUi.LabelledValue("Tower ID", module.TowerRun.Hash);
            if (state == UiState.Hovered)
            {
                ImGui.SetTooltip("This is unique to you.");
            }
        }, "塔内セッション情報");
    }
}
