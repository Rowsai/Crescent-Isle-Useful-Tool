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

            if (module.NextSpawnUtc is { } nextSpawnUtc)
            {
                var remaining = nextSpawnUtc - DateTime.UtcNow;
                remaining = remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;

                ImGui.TextDisabled(module.T("panel.next_spawn"));
                ImGui.SameLine();
                ImGui.TextColored(CrescentTheme.AccentSoft, remaining.ToString(@"mm\:ss"));
                ImGui.SameLine();
                ImGui.TextDisabled(module.HasObservedSpawnTime ? "（FATE開始時刻から算出）" : "（インスタンス時間から推定）");
                if (remaining == TimeSpan.Zero)
                {
                    ImGui.TextColored(CrescentTheme.Warning, module.T("panel.due"));
                }
            }

            if (module.Config.ShouldEnableTreasureSearchMode)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                CrescentTheme.Status(
                    "マジックポット宝箱探索",
                    module.IsTreasureSearchActive ? "実行中" : "待機中",
                    module.IsTreasureSearchActive ? CrescentTheme.Warning : CrescentTheme.AccentSoft
                );
                ImGui.TextWrapped(module.TreasureSearchStatus);
                if (module.TreasureSearchTarget is { } target)
                {
                    ImGui.TextDisabled($"推定地点 X:{target.X:F1} Y:{target.Z:F1} / ヒント {module.TreasureSearchHintCount}件");
                }
            }
        }, "観測できたFATEの開始時刻を優先し、次回を30分後として表示します。未観測時だけインスタンス時間で補完します。", CrescentTheme.Warning);
    }
}
