using System;
using BOCCHI.Data;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace BOCCHI.Modules.MagicPot;

public class Panel
{
    public void Draw(MagicPotModule module)
    {
        if (!ZoneData.IsInNorthHorn())
        {
            return;
        }

        OcelotUi.Title($"{module.T("panel.title")}:");
        OcelotUi.Indent(() =>
        {
            if (module.IsNorthPotActive)
            {
                ImGui.TextColored(BOCCHI.Ui.CrescentTheme.AccentSoft, module.T("panel.active"));
                return;
            }

            var nextSpawnUtc = module.NextSpawnUtc;
            if (nextSpawnUtc == null)
            {
                ImGui.TextDisabled(module.T("panel.awaiting_observation"));
                return;
            }

            var remaining = nextSpawnUtc.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                ImGui.TextColored(BOCCHI.Ui.CrescentTheme.AccentSoft, module.T("panel.due"));
                return;
            }

            OcelotUi.LabelledValue(module.T("panel.next_spawn"), remaining.ToString(@"mm\:ss"));
        });
    }
}
