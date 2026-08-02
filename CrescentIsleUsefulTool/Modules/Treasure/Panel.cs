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
                    if (ImGui.BeginTable("##TreasureRouteCounts", 4, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
                    {
                        DrawRouteCount("全地点", module.Hunter.ExtractedLocationCount, CrescentTheme.AccentSoft);
                        DrawRouteCount("巡回済み", module.Hunter.CompletedLocationCount, CrescentTheme.Success);
                        DrawRouteCount("残り", module.Hunter.RemainingLocationCount, CrescentTheme.Warning);
                        DrawRouteCount("到達不可", module.Hunter.UnreachableLocationCount, CrescentTheme.Danger);
                        ImGui.EndTable();
                    }

                    ImGui.TextColored(TreasureModule.Bronze, $"座標内訳：青銅 {module.Hunter.ExtractedBronzeCount}");
                    ImGui.SameLine();
                    ImGui.TextColored(TreasureModule.Silver, $"白銀 {module.Hunter.ExtractedSilverCount}");
                    if (module.Hunter.ExcludedUndergroundLocationCount > 0)
                    {
                        ImGui.TextColored(CrescentTheme.Muted, $"地下空洞 {module.Hunter.ExcludedUndergroundLocationCount}地点は巡回対象外です。");
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
        ImGui.TextColored(CrescentTheme.AccentSoft, "マギ・トレジャーサーチ開始時結果");
        if (!module.Hunter.RouteStartRemainingChestCount.HasValue)
        {
            ImGui.TextColored(CrescentTheme.Muted, "開始操作後に青銅・白銀それぞれの残数を取得します。");
            return;
        }

        if (ImGui.BeginTable("##RouteStartTreasureCounts", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            DrawCount("残り青銅", module.Hunter.RouteStartBronzeChestCount ?? 0, 30, TreasureModule.Bronze, false);
            DrawCount("残り白銀", module.Hunter.RouteStartSilverChestCount ?? 0, 8, TreasureModule.Silver, false);
            ImGui.TableNextColumn();
            ImGui.TextDisabled("残り合計");
            ImGui.TextColored(CrescentTheme.AccentSoft, $"{module.Hunter.RouteStartRemainingChestCount.Value}個");
            ImGui.EndTable();
        }

        ImGui.TextDisabled($"この巡回で開封：{module.Hunter.SurfaceOpenedThisRun}個");
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
            CrescentTheme.EmptyState(module.Tracker.CountMeasurementFailed
                ? "マギ・トレジャーサーチの結果を取得できませんでした。次回使用時に再取得します。"
                : "マギ・トレジャーサーチの青銅・白銀残数を待っています。");
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

        if (module.Tracker.CountMeasurementPending)
        {
            ImGui.TextColored(CrescentTheme.AccentSoft, "● マギ・トレジャーサーチで残数を再計測中です。");
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

    private static void DrawRouteCount(string label, int value, Vector4 color)
    {
        ImGui.TableNextColumn();
        ImGui.TextDisabled(label);
        ImGui.TextColored(color, value.ToString());
    }
}
