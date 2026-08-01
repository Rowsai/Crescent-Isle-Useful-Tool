using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CrescentIsleUsefulTool.Modules.Treasure;

public class TreasureTracker : IDisposable
{
    private const int MaximumBronzeChests = 30;

    private const int MaximumSilverChests = 8;

    private readonly HashSet<ulong> recordedOpenedTreasures = [];

    private int observedAcquiredBronzeChests;

    private int observedAcquiredSilverChests;

    public List<Treasure> Treasures { get; private set; } = [];

    public bool CountInitialised { get; private set; } = false;

    public int BronzeChests { get; private set; } = 0;

    public int SilverChests { get; private set; } = 0;

    public int RemainingChests => BronzeChests + SilverChests;

    public int AcquiredBronzeChests => CountInitialised
        ? Math.Max(observedAcquiredBronzeChests, MaximumBronzeChests - BronzeChests)
        : observedAcquiredBronzeChests;

    public int AcquiredSilverChests => CountInitialised
        ? Math.Max(observedAcquiredSilverChests, MaximumSilverChests - SilverChests)
        : observedAcquiredSilverChests;

    private readonly TimeSpan ParseWideTextCooldown = TimeSpan.FromSeconds(5);

    private DateTime LastParseWideText = DateTime.MinValue;

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
            .OrderBy(treasure => Player.DistanceTo(treasure.Position))
            .ToList();
    }

    public void RecordAcquired(ulong entityId, TreasureType treasureType)
    {
        if (!recordedOpenedTreasures.Add(entityId))
        {
            return;
        }

        if (treasureType == TreasureType.Bronze)
        {
            observedAcquiredBronzeChests++;
            BronzeChests = Math.Max(0, BronzeChests - 1);
        }
        else if (treasureType == TreasureType.Silver)
        {
            observedAcquiredSilverChests++;
            SilverChests = Math.Max(0, SilverChests - 1);
        }
    }

    public void ResetSession()
    {
        Treasures.Clear();
        recordedOpenedTreasures.Clear();
        observedAcquiredBronzeChests = 0;
        observedAcquiredSilverChests = 0;
        BronzeChests = 0;
        SilverChests = 0;
        CountInitialised = false;
        LastParseWideText = DateTime.MinValue;
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

        var timeSinceLast = DateTime.Now - LastParseWideText;
        if (timeSinceLast < ParseWideTextCooldown)
        {
            return;
        }

        LastParseWideText = DateTime.Now;

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

        var pattern = LogMessageHelper.GetLogMessagePattern(10965);
        var text = textNode->NodeText.ToString();
        var match = Regex.Match(text, pattern);

        if (!match.Success)
        {
            return;
        }

        if (!int.TryParse(match.Groups[1].Value, out var silver) ||
            !int.TryParse(match.Groups[2].Value, out var bronze))
        {
            return;
        }

        SilverChests = silver;
        BronzeChests = bronze;
        CountInitialised = true;
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
    }
}
