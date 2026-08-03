using System.Numerics;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;
using ECommons.GameHelpers;

namespace CrescentIsleUsefulTool.Modules.Treasure;

public class Panel
{
    public void Draw(TreasureModule module)
    {
        CrescentTheme.Card("Treasure", module.T("panel.title"), () =>
        {
            DrawMeasuredCounts(module);

            if (ZoneData.IsInNorthHorn())
            {
                ImGui.Spacing();
                DrawHunterProgress(module);
                ImGui.Spacing();
                DrawInternalLayoutCounts(module);
            }

            ImGui.Spacing();
            DrawNearbyCoffers(module);
        }, "マギ・トレジャーサーチの測定値と通常宝箱の巡回状況を分けて表示します。", TreasureModule.Bronze);
    }

    private static void DrawMeasuredCounts(TreasureModule module)
    {
        ImGui.TextDisabled("マギ・トレジャーサーチ 最終測定（Action ID 41651）");
        if (!module.Tracker.CountInitialised)
        {
            CrescentTheme.EmptyState(module.Tracker.CountMeasurementFailed
                ? "測定結果を取得できませんでした。次回の使用時に再測定します。"
                : "まだ測定されていません。ハント開始時、拠点エーテライト付近で測定します。");
        }
        else if (ImGui.BeginTable("##MeasuredTreasureCounts", 3,
                     ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            DrawCount("残り青銅", module.Tracker.BronzeChests, 30, TreasureModule.Bronze,
                module.Config.ShowPercentageActiveTreasureCount);
            DrawCount("残り白銀", module.Tracker.SilverChests, 8, TreasureModule.Silver,
                module.Config.ShowPercentageActiveTreasureCount);
            ImGui.TableNextColumn();
            ImGui.TextDisabled("残り合計");
            ImGui.TextColored(CrescentTheme.AccentSoft, $"{module.Tracker.RemainingChests}個");
            ImGui.EndTable();

            if (module.Tracker.LastMeasurementUtc.HasValue)
            {
                ImGui.TextDisabled(
                    $"最終取得: {module.Tracker.LastMeasurementUtc.Value.ToLocalTime():HH:mm:ss}（測定後の推測減算は行いません）");
            }
        }

        if (module.Tracker.CountMeasurementPending)
        {
            ImGui.TextColored(CrescentTheme.AccentSoft, "● 測定結果を取得中です。");
        }
        else if (module.Tracker.CountMeasurementFailed && module.Tracker.CountInitialised)
        {
            ImGui.TextColored(
                CrescentTheme.Warning,
                "直近の再測定結果を取得できなかったため、前回の実測値を表示しています。");
        }
    }

    private static void DrawHunterProgress(TreasureModule module)
    {
        ImGui.TextDisabled("トレジャーハンター進行（確認済み実績）");
        if (ImGui.BeginTable("##HunterTreasureProgress", 4,
                ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            DrawRouteCount("取得 青銅", module.Hunter.HunterOpenedBronzeCount, TreasureModule.Bronze);
            DrawRouteCount("取得 白銀", module.Hunter.HunterOpenedSilverCount, TreasureModule.Silver);
            DrawRouteCount("取得 合計", module.Hunter.SurfaceOpenedThisRun, CrescentTheme.Success);
            DrawRouteCount("巡回残り", module.Hunter.RemainingLocationCount, CrescentTheme.Warning);
            ImGui.EndTable();
        }

        ImGui.TextDisabled(
            $"確認済み {module.Hunter.CompletedLocationCount}（箱なし・既開封 {module.Hunter.CheckedWithoutCofferCount}） / " +
            $"到達不可 {module.Hunter.UnreachableLocationCount} / 危険除外 {module.Hunter.UnsafeLocationCount}");
    }

    private static void DrawInternalLayoutCounts(TreasureModule module)
    {
        ImGui.TextDisabled("ゲーム内部データの座標分類");
        if (module.Hunter.InternalRandomCofferLocationCount == 0)
        {
            ImGui.TextUnformatted("ハント開始時に現在の内部レイアウトを検証します。");
            return;
        }

        if (ImGui.BeginTable("##InternalTreasurePlacements", 4,
                ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            DrawRouteCount("通常箱 全座標", module.Hunter.InternalRandomCofferLocationCount, CrescentTheme.AccentSoft);
            DrawRouteCount("地上巡回", module.Hunter.ExtractedLocationCount, CrescentTheme.Success);
            DrawRouteCount("地下除外", module.Hunter.ExcludedUndergroundLocationCount, CrescentTheme.Warning);
            DrawRouteCount("ポット箱除外", module.Hunter.ExcludedMagicPotLocationCount, CrescentTheme.Muted);
            ImGui.EndTable();
        }

        ImGui.TextColored(TreasureModule.Bronze,
            $"内部座標内訳: 青銅 {module.Hunter.InternalBronzeLocationCount}");
        ImGui.SameLine();
        ImGui.TextColored(TreasureModule.Silver,
            $"白銀 {module.Hunter.InternalSilverLocationCount}");
        ImGui.TextDisabled(
            "地下件数は、青銅・白銀の内部行を検証した後に地下座標へ分類した実数です。");
    }

    private static void DrawNearbyCoffers(TreasureModule module)
    {
        ImGui.TextDisabled("現在地付近で検出した通常宝箱");
        if (module.Treasures.Count == 0)
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

            var position = treasure.GetPosition();
            ImGui.TextUnformatted(treasure.GetName());
            ImGui.SameLine();
            ImGui.TextDisabled(
                $"X:{position.X:F1} Y:{position.Z:F1} / {Vector3.Distance(Player.Position, position):F1}m");
        }
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
