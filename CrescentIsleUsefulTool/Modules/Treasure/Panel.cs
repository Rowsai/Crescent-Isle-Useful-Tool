using System.Numerics;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ui;
using ECommons.GameHelpers;
using Dalamud.Bindings.ImGui;

namespace CrescentIsleUsefulTool.Modules.Treasure;

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

                    ImGui.TextDisabled($"巡回済み {module.Hunter.CompletedLocationCount} / 残り {module.Hunter.RemainingLocationCount}");
                    if (module.Hunter.UnreachableLocationCount > 0)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(CrescentTheme.Warning, $"到達不可 {module.Hunter.UnreachableLocationCount}");
                    }
                    if (module.Hunter.ExcludedUndergroundLocationCount > 0)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(
                            CrescentTheme.Muted,
                            $"地下空洞 {module.Hunter.ExcludedUndergroundLocationCount}件を除外"
                        );
                    }

                    ImGui.Spacing();
                    DrawSurfaceCountValidation(module);
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

    private static void DrawSurfaceCountValidation(TreasureModule module)
    {
        ImGui.TextDisabled("マギ・トレジャーサーチ照合");
        if (!module.Hunter.RouteStartRemainingChestCount.HasValue)
        {
            ImGui.TextColored(CrescentTheme.Muted, "開始時に残り宝箱数を取得します。");
            return;
        }

        ImGui.TextUnformatted($"開始時 {module.Hunter.RouteStartRemainingChestCount.Value}個 / 地上で取得 {module.Hunter.SurfaceOpenedThisRun}個");
        if (!module.Hunter.SurfaceCountValidationCompleted)
        {
            ImGui.TextColored(CrescentTheme.AccentSoft, $"地上ルート巡回中（現在の残数 {module.Tracker.RemainingChests}個）");
            return;
        }

        if (module.Tracker.RemainingChests == 0)
        {
            ImGui.TextColored(CrescentTheme.Success, "地下空洞を除く地上巡回で、残数0を確認しました。");
            return;
        }

        ImGui.TextColored(
            CrescentTheme.Warning,
            $"地上全座標の巡回後も {module.Tracker.RemainingChests}個残っています（地下空洞または未取得）。");
    }

    private void DrawActiveChests(TreasureModule module)
    {
        if (!module.Tracker.CountInitialised)
        {
            CrescentTheme.EmptyState("取得可能な宝箱数を計測しています。");
        }
        else if (ImGui.BeginTable("##TreasureCounts", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            DrawCount(module.T("panel.active_bronze.label"), module.Tracker.BronzeChests, 30, TreasureModule.Bronze, module.Config.ShowPercentageActiveTreasureCount);
            DrawCount(module.T("panel.active_silver.label"), module.Tracker.SilverChests, 8, TreasureModule.Silver, module.Config.ShowPercentageActiveTreasureCount);
            ImGui.TableNextColumn();
            ImGui.TextDisabled(module.T("panel.remaining.label"));
            ImGui.TextColored(CrescentTheme.AccentSoft, module.Tracker.RemainingChests.ToString());
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("この探索で取得済み");
        ImGui.SameLine();
        ImGui.TextColored(TreasureModule.Bronze, $"青銅 {module.Tracker.AcquiredBronzeChests}");
        ImGui.SameLine();
        ImGui.TextColored(TreasureModule.Silver, $"白銀 {module.Tracker.AcquiredSilverChests}");
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
