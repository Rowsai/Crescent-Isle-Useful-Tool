using System.Numerics;
using BOCCHI.Data;
using BOCCHI.Ui;
using ECommons.GameHelpers;
using Dalamud.Bindings.ImGui;

namespace BOCCHI.Modules.Treasure;

public class Panel
{
    public void Draw(TreasureModule module)
    {
        CrescentTheme.Card("Treasure", module.T("panel.title"), () =>
        {
            DrawActiveChests(module);

            if (ZoneData.IsInNorthHorn())
            {
                ImGui.Spacing();
                ImGui.TextDisabled("内部データから取得した巡回地点");
                if (module.Hunter.ExtractedLocationCount == 0)
                {
                    ImGui.TextUnformatted("ハント開始時に座標を取得します。");
                }
                else
                {
                    ImGui.TextColored(TreasureModule.Bronze, $"青銅 {module.Hunter.ExtractedBronzeCount}");
                    ImGui.SameLine();
                    ImGui.TextColored(TreasureModule.Silver, $"白銀 {module.Hunter.ExtractedSilverCount}");
                    ImGui.SameLine();
                    ImGui.TextDisabled($"合計 {module.Hunter.ExtractedLocationCount}");
                }
            }

            ImGui.Spacing();

            if (module.Treasures.Count <= 0)
            {
                CrescentTheme.EmptyState(module.T("panel.none"));
                return;
            }

            foreach (var treasure in module.Treasures)
            {
                if (!treasure.IsValid())
                {
                    continue;
                }

                var pos = treasure.GetPosition();
                ImGui.TextUnformatted(treasure.GetName());
                ImGui.SameLine();
                ImGui.TextDisabled($"X:{pos.X:F1} Y:{pos.Z:F1}  /  {Vector3.Distance(Player.Position, pos):F1}m");
            }
        }, "取得可能数と現在地付近の宝箱を表示します。", TreasureModule.Bronze);
    }

    private void DrawActiveChests(TreasureModule module)
    {
        if (!module.Tracker.CountInitialised)
        {
            CrescentTheme.EmptyState("取得可能な宝箱数を計測しています。");
            return;
        }

        if (!ImGui.BeginTable("##TreasureCounts", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            return;
        }

        DrawCount(module.T("panel.active_bronze.label"), module.Tracker.BronzeChests, 30, TreasureModule.Bronze, module.Config.ShowPercentageActiveTreasureCount);
        DrawCount(module.T("panel.active_silver.label"), module.Tracker.SilverChests, 8, TreasureModule.Silver, module.Config.ShowPercentageActiveTreasureCount);
        ImGui.TableNextColumn();
        ImGui.TextDisabled(module.T("panel.remaining.label"));
        ImGui.TextColored(CrescentTheme.AccentSoft, module.Tracker.RemainingChests.ToString());
        ImGui.EndTable();
    }

    private static void DrawCount(string label, int value, int maximum, Vector4 color, bool showPercentage)
    {
        ImGui.TableNextColumn();
        ImGui.TextDisabled(label);
        ImGui.TextColored(color, $"{value} / {maximum}");
        if (showPercentage)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"({value / (float)maximum * 100f:F1}%)");
        }
    }
}
