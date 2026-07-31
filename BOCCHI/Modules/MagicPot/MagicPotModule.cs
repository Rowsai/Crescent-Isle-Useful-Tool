using System;
using System.Collections.Generic;
using System.Linq;
using BOCCHI.Data;
using Dalamud.Game.ClientState.Fates;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Modules;
using Ocelot.Windows;

namespace BOCCHI.Modules.MagicPot;

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

    public override MagicPotConfig Config => PluginConfig.MagicPotConfig;

    public override bool IsEnabled => Config.IsPropertyEnabled(nameof(Config.Enabled));

    public override bool ShouldUpdate => true;

    public bool IsNorthPotActive => activeNorthPotIds.Count > 0;

    public DateTime NextSpawnUtc => lastNorthPotSpawnUtc?.AddSeconds(RespawnIntervalSeconds)
                                    ?? estimatedNextSpawnUtc
                                    ?? DateTime.UtcNow.AddSeconds(FirstSpawnDelaySeconds);

    public int? OldestPlayerTimeMinutes { get; private set; }

    public MagicPotModule(Plugin plugin, Config config)
        : base(plugin, config)
    {
    }

    public override unsafe void Update(UpdateContext context)
    {
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
            if (activeNorthPotIds.Add((uint)fate.FateId))
            {
                lastNorthPotSpawnUtc = DateTime.UtcNow;
                estimatedNextSpawnUtc = lastNorthPotSpawnUtc.Value.AddSeconds(RespawnIntervalSeconds);
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

    public override void OnTerritoryChanged(uint id)
    {
        activeNorthPotIds.Clear();
        lastNorthPotSpawnUtc = null;
        previousContentTimeLeftSeconds = null;
        OldestPlayerTimeMinutes = null;
        estimatedNextSpawnUtc = id == ZoneData.NORTHHORN
            ? DateTime.UtcNow.AddSeconds(FirstSpawnDelaySeconds)
            : null;
    }
}
