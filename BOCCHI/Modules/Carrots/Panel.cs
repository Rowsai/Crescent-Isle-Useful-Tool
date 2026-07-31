using BOCCHI.Ui;
using Dalamud.Bindings.ImGui;

namespace BOCCHI.Modules.Carrots;

public class Panel
{
    public void Draw(CarrotsModule module)
    {
        CrescentTheme.Card("Carrots", module.T("panel.title"), () =>
        {
            if (module.carrots.Count <= 0)
            {
                CrescentTheme.EmptyState(module.T("panel.none"));
                return;
            }

            foreach (var carrot in module.carrots)
            {
                if (!carrot.IsValid())
                {
                    continue;
                }

                var pos = carrot.GetPosition();
                ImGui.TextUnformatted(module.T("panel.label"));
                ImGui.SameLine();
                ImGui.TextDisabled($"X:{pos.X:F1} Y:{pos.Z:F1}");
            }
        }, "近くのにんじんを検知します。");
    }
}
