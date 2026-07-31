using System;
using BOCCHI.Data;
using BOCCHI.Ui;
using Dalamud.Bindings.ImGui;

namespace BOCCHI.Modules.MagicPot;

public class Panel
{
    public void Draw(MagicPotModule module)
    {
        if (!ZoneData.IsInNorthHorn())
        {
            return;
        }

        CrescentTheme.Card("MagicPot", module.T("panel.title"), () =>
        {
            if (module.IsNorthPotActive)
            {
                CrescentTheme.Status("検知状態", module.T("panel.active"), CrescentTheme.Warning);
            }
            else
            {
                CrescentTheme.Status("検知状態", "次回発生を監視中", CrescentTheme.AccentSoft);
            }

            ImGui.Spacing();
            if (module.OldestPlayerTimeMinutes != null)
            {
                ImGui.TextDisabled("基準となる最古時間");
                ImGui.SameLine();
                ImGui.TextUnformatted($"{module.OldestPlayerTimeMinutes}分");
            }

            var remaining = module.NextSpawnUtc - DateTime.UtcNow;
            remaining = remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;

            ImGui.TextDisabled(module.T("panel.next_spawn"));
            ImGui.SameLine();
            ImGui.TextColored(CrescentTheme.AccentSoft, remaining.ToString(@"mm\:ss"));
            if (remaining == TimeSpan.Zero)
            {
                ImGui.SameLine();
                ImGui.TextColored(CrescentTheme.Warning, module.T("panel.due"));
            }
        }, "初回は最古時間179分を基準に20分後、以降は30分周期で推定します。", CrescentTheme.Warning);
    }
}
