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
        var treasures = Svc.Objects
            .Where(o => o is { ObjectKind: ObjectKind.Treasure })
            .ToDictionary(o => (ulong)o.EntityId, o => o);

        var knownIds = Treasures.Select(t => t.Id).ToHashSet();

        // Removed
        for (var i = Treasures.Count - 1; i >= 0; i--)
        {
            var treasure = Treasures[i];
            if (!treasures.ContainsKey(treasure.Id) || !treasure.IsValid())
            {
                Treasures.RemoveAt(i);
            }
        }

        // Added
        foreach (var (objectId, obj) in treasures)
        {
            if (knownIds.Contains(objectId))
            {
                continue;
            }

            var treasure = new Treasure(obj);
            if (treasure.IsValid())
            {
                Treasures.Add(treasure);
            }
        }

        Treasures = Treasures.OrderBy(t => Player.DistanceTo(t.GetPosition())).ToList();

        foreach (var treasure in Treasures)
        {
            if (treasure.CheckOpened())
            {
                RecordAcquired(treasure.Id, treasure.GetTreasureType());
            }
        }
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

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (!addon->IsVisible)
        {
            return;
        }

        var timeSinceLast = DateTime.Now - LastParseWideText;
        if (timeSinceLast < ParseWideTextCooldown)
        {
            return;
        }

        LastParseWideText = DateTime.Now;

        var pattern = LogMessageHelper.GetLogMessagePattern(10965);
        var text = addon->GetNodeById(3)->GetAsAtkTextNode()->NodeText.ToString();
        var match = Regex.Match(text, pattern);

        if (!match.Success)
        {
            return;
        }

        SilverChests = int.Parse(match.Groups[1].Value);
        BronzeChests = int.Parse(match.Groups[2].Value);
        CountInitialised = true;
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
    }
}
