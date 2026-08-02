using System;
using System.Collections.Generic;
using System.Linq;
using CrescentIsleUsefulTool.Data;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Modules;
using Ocelot.Windows;

namespace CrescentIsleUsefulTool.Modules.MagicPot;

[OcelotModule(1002, 5)]
public class MagicPotModule : Module
{
    private const float FreshInstanceReferenceSeconds = 179f * 60f;

    private const float FirstSpawnDelaySeconds = 20f * 60f;

    private const float RespawnIntervalSeconds = 30f * 60f;

    private readonly HashSet<uint> activeNorthPotIds = [];

    private DateTime? lastNorthPotSpawnUtc;

    private DateTime? estimatedNextSpawnUtc;

    private float? previousContentTimeLeftSeconds;

    private readonly Panel panel = new();

    private readonly MagicPotTreasureHunter treasureHunter;

    public override MagicPotConfig Config => PluginConfig.MagicPotConfig;

    public override bool IsEnabled => Config.IsPropertyEnabled(nameof(Config.Enabled));

    public override bool ShouldUpdate => true;

    public bool IsNorthPotActive => activeNorthPotIds.Count > 0;

    public DateTime? NextSpawnUtc
    {
        get
        {
            if (lastNorthPotSpawnUtc is not { } observed)
            {
                return estimatedNextSpawnUtc;
            }

            var elapsed = Math.Max(0d, (DateTime.UtcNow - observed).TotalSeconds);
            var cycle = Math.Max(1d, Math.Floor(elapsed / RespawnIntervalSeconds));
            var expected = observed.AddSeconds(cycle * RespawnIntervalSeconds);
            // Keep the just-reached prediction visible briefly while the FATE
            // list catches up instead of rolling to the following 30-minute
            // cycle a fraction of a second before the spawn is observed.
            return expected < DateTime.UtcNow.AddMinutes(-2)
                ? expected.AddSeconds(RespawnIntervalSeconds)
                : expected;
        }
    }

    public bool HasObservedSpawnTime => lastNorthPotSpawnUtc != null;

    public uint? MostRecentPotFateId { get; private set; }

    public bool IsTreasureSearchActive => treasureHunter.IsActive;

    public string TreasureSearchStatus => treasureHunter.RuntimeStatus;

    public System.Numerics.Vector3? TreasureSearchTarget => treasureHunter.Target;

    public int TreasureSearchHintCount => treasureHunter.HintCount;

    public int? OldestPlayerTimeMinutes { get; private set; }

    public MagicPotModule(Plugin plugin, Config config)
        : base(plugin, config)
    {
        treasureHunter = new MagicPotTreasureHunter(this);
    }

    public override unsafe void Update(UpdateContext context)
    {
        treasureHunter.Update();

        if (!ZoneData.IsInNorthHorn())
        {
            activeNorthPotIds.Clear();
            return;
        }

        UpdateInstanceEstimate();

        var activePots = Svc.Fates.Where(IsNorthMagicPot).ToList();
        var activeIds = activePots.Select(fate => (uint)fate.FateId).ToHashSet();

        foreach (var fate in activePots)
        {
            activeNorthPotIds.Add((uint)fate.FateId);

            // EurekaTrackerAutoPopper records IFate.StartTimeEpoch and calculates
            // the next alternating pot occurrence from the most recent spawn +30m.
            // Keep the same precise local observation without uploading player data.
            if (fate.StartTimeEpoch > 0)
            {
                var observedUtc = DateTimeOffset.FromUnixTimeSeconds(fate.StartTimeEpoch).UtcDateTime;
                if (observedUtc <= DateTime.UtcNow.AddMinutes(1)
                    && (lastNorthPotSpawnUtc == null || observedUtc > lastNorthPotSpawnUtc.Value))
                {
                    lastNorthPotSpawnUtc = observedUtc;
                    MostRecentPotFateId = (uint)fate.FateId;
                    estimatedNextSpawnUtc = observedUtc.AddSeconds(RespawnIntervalSeconds);
                }
            }
        }

        activeNorthPotIds.IntersectWith(activeIds);
    }

    private unsafe void UpdateInstanceEstimate()
    {
        var content = PublicContentOccultCrescent.GetInstance();
        if (content == null || content->ContentTimeLeft <= 0f)
        {
            estimatedNextSpawnUtc ??= DateTime.UtcNow.AddSeconds(FirstSpawnDelaySeconds);
            return;
        }

        var timeLeft = content->ContentTimeLeft;

        // A large upward jump means the player entered a new instance. Do not
        // carry a spawn observation from the previous instance into this one.
        if (previousContentTimeLeftSeconds != null && timeLeft > previousContentTimeLeftSeconds.Value + 60f)
        {
            lastNorthPotSpawnUtc = null;
            MostRecentPotFateId = null;
            activeNorthPotIds.Clear();
        }

        previousContentTimeLeftSeconds = timeLeft;
        OldestPlayerTimeMinutes = (int)Math.Ceiling(timeLeft / 60f);

        if (lastNorthPotSpawnUtc != null)
        {
            estimatedNextSpawnUtc = lastNorthPotSpawnUtc.Value.AddSeconds(RespawnIntervalSeconds);
            return;
        }

        // The timer model requested for North Horn:
        //   oldest displayed time 179m -> first spawn in 20m
        //   every later spawn -> 30m cadence
        var elapsedFromReference = Math.Max(0f, FreshInstanceReferenceSeconds - timeLeft);
        var untilFirstSpawn = FirstSpawnDelaySeconds - elapsedFromReference;
        float secondsUntilSpawn;

        if (untilFirstSpawn >= 0f)
        {
            secondsUntilSpawn = untilFirstSpawn;
        }
        else
        {
            var secondsSinceFirstSpawn = -untilFirstSpawn;
            var phase = secondsSinceFirstSpawn % RespawnIntervalSeconds;
            secondsUntilSpawn = phase < 1f ? 0f : RespawnIntervalSeconds - phase;
        }

        estimatedNextSpawnUtc = DateTime.UtcNow.AddSeconds(secondsUntilSpawn);
    }

    private static bool IsNorthMagicPot(IFate fate)
    {
        return NorthHornContent.IsMagicPotFate(fate.FateId) || EventData.GetFate(fate.FateId).IsPot;
    }

    public override bool RenderMainUi(RenderContext context)
    {
        panel.Draw(this);
        return true;
    }

    public override void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
    {
        treasureHunter.OnChatMessage(type, timestamp, sender, message, isHandled);
    }

    public override void OnTerritoryChanged(uint id)
    {
        activeNorthPotIds.Clear();
        lastNorthPotSpawnUtc = null;
        MostRecentPotFateId = null;
        previousContentTimeLeftSeconds = null;
        OldestPlayerTimeMinutes = null;
        estimatedNextSpawnUtc = id == ZoneData.NORTHHORN
            ? DateTime.UtcNow.AddSeconds(FirstSpawnDelaySeconds)
            : null;
        treasureHunter.ResetForTerritoryChange();
    }
}
