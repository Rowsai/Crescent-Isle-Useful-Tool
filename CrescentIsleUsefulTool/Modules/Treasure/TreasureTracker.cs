using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CrescentIsleUsefulTool.Modules.Treasure;

public class TreasureTracker : IDisposable
{
    private const int MaximumBronzeChests = 30;

    private const int MaximumSilverChests = 8;

    private string? countMessagePattern;

    public List<Treasure> Treasures { get; private set; } = [];

    public bool CountInitialised { get; private set; } = false;

    public bool CountMeasurementPending { get; private set; }

    public bool CountMeasurementFailed { get; private set; }

    public int CountRevision { get; private set; }

    public DateTime? LastMeasurementUtc { get; private set; }

    public int BronzeChests { get; private set; } = 0;

    public int SilverChests { get; private set; } = 0;

    public int RemainingChests => BronzeChests + SilverChests;

    private readonly TimeSpan ParseWideTextCooldown = TimeSpan.FromSeconds(5);

    private DateTime LastParseWideText = DateTime.MinValue;

    private DateTime MeasurementRequestedAtUtc = DateTime.MinValue;

    public TreasureTracker()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
    }

    public void Tick(Plugin plugin)
    {
        Treasures = Svc.Objects
            .Where(obj => obj is { ObjectKind: ObjectKind.Treasure, IsDead: false, IsTargetable: true })
            .Where(obj => obj.IsValid())
            .Select(obj => new Treasure(obj))
            // Magic-pot coffers and unrelated treasure objects are managed by
            // separate modules and must never enter the normal hunter list.
            .Where(treasure => treasure.GetTreasureType() is TreasureType.Bronze or TreasureType.Silver)
            .OrderBy(treasure => Player.DistanceTo(treasure.Position))
            .ToList();

        if (CountMeasurementPending && DateTime.UtcNow - MeasurementRequestedAtUtc > TimeSpan.FromSeconds(10))
        {
            CountMeasurementPending = false;
            CountMeasurementFailed = true;
        }
    }

    public void BeginCountMeasurement()
    {
        // Keep the last confirmed values visible while a new measurement is
        // pending. Clearing them here made the panel look permanently empty
        // whenever the one-frame wide-text event was missed.
        CountMeasurementPending = true;
        CountMeasurementFailed = false;
        LastParseWideText = DateTime.MinValue;
        MeasurementRequestedAtUtc = DateTime.UtcNow;
    }

    public void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
    {
        if (ZoneData.IsInOccultCrescent())
        {
            TryUpdateCounts(message.TextValue);
        }
    }

    public void ResetSession()
    {
        Treasures.Clear();
        BronzeChests = 0;
        SilverChests = 0;
        CountInitialised = false;
        CountMeasurementPending = false;
        CountMeasurementFailed = false;
        CountRevision = 0;
        LastMeasurementUtc = null;
        LastParseWideText = DateTime.MinValue;
        MeasurementRequestedAtUtc = DateTime.MinValue;
    }

    private unsafe void OnWideTextPostDraw(AddonEvent type, AddonArgs args)
    {
        if (!ZoneData.IsInOccultCrescent())
        {
            return;
        }

        if (args.Addon.Address == nint.Zero)
        {
            return;
        }

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || !addon->IsVisible)
        {
            return;
        }

        if (DateTime.UtcNow - MeasurementRequestedAtUtc < TimeSpan.FromMilliseconds(250))
        {
            return;
        }

        var timeSinceLast = DateTime.Now - LastParseWideText;
        if (timeSinceLast < ParseWideTextCooldown)
        {
            return;
        }

        var node = addon->GetNodeById(3);
        if (node == null)
        {
            return;
        }

        var textNode = node->GetAsAtkTextNode();
        if (textNode == null)
        {
            return;
        }

        var text = textNode->NodeText.ToString();
        TryUpdateCounts(text);
    }

    private bool TryUpdateCounts(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        countMessagePattern ??= LogMessageHelper.GetLogMessagePattern(10965);
        var match = Regex.Match(text, countMessagePattern);

        var silver = -1;
        var bronze = -1;
        var parsed = match.Success &&
                     TryReadCount(match, "lnum1", 1, out silver) &&
                     TryReadCount(match, "lnum2", 2, out bronze);

        if (!parsed)
        {
            // Fallback for resolved/translated SeStrings. The Treasuresight
            // result contains exactly the silver count followed by bronze.
            var numbers = Regex.Matches(text, @"\d+")
                .Select(value => int.TryParse(value.Value, out var number) ? number : -1)
                .Where(value => value >= 0)
                .ToArray();
            parsed = numbers.Length >= 2 &&
                     (text.Contains("銀", StringComparison.Ordinal) || text.Contains("silver", StringComparison.OrdinalIgnoreCase)) &&
                     (text.Contains("銅", StringComparison.Ordinal) || text.Contains("bronze", StringComparison.OrdinalIgnoreCase));
            if (parsed)
            {
                silver = numbers[0];
                bronze = numbers[1];
            }
        }

        if (!parsed)
        {
            return false;
        }

        if (silver is < 0 or > MaximumSilverChests || bronze is < 0 or > MaximumBronzeChests)
        {
            return false;
        }

        SilverChests = silver;
        BronzeChests = bronze;
        CountInitialised = true;
        CountMeasurementPending = false;
        CountMeasurementFailed = false;
        CountRevision++;
        LastMeasurementUtc = DateTime.UtcNow;
        LastParseWideText = DateTime.Now;
        return true;
    }

    private static bool TryReadCount(Match match, string groupName, int fallbackIndex, out int value)
    {
        value = -1;
        var group = match.Groups[groupName];
        if (group.Success && int.TryParse(group.Value, out value))
        {
            return true;
        }

        return match.Groups.Count > fallbackIndex && int.TryParse(match.Groups[fallbackIndex].Value, out value);
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
    }
}
