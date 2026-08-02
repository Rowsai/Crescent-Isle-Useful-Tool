using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace CrescentIsleUsefulTool.Modules.ForkedTower;

public class Panel
{
    public void Draw(ForkedTowerModule module)
    {
        if (!ZoneData.IsInForkedTower())
        {
            return;
        }

        CrescentTheme.Card("ForkedTower", "フォークタワー", () =>
        {
            var state = OcelotUi.LabelledValue("塔内セッションID", module.TowerRun.Hash);
            if (state == UiState.Hovered)
            {
                ImGui.SetTooltip("プレイヤーごとに異なる識別情報です。");
            }
        }, "塔内セッション情報");
    }
}
