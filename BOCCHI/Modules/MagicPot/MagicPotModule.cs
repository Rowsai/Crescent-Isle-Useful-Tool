using System;
using System.Collections.Generic;
using System.Linq;
using BOCCHI.Data;
using Dalamud.Game.ClientState.Fates;
using ECommons.DalamudServices;
using Ocelot.Modules;
using Ocelot.Windows;

namespace BOCCHI.Modules.MagicPot;

[OcelotModule(1002, 5)]
public class MagicPotModule : Module
{
    private readonly HashSet<uint> activeNorthPotIds = [];

    private DateTime? lastNorthPotSpawnUtc;

    private readonly Panel panel = new();

    public override MagicPotConfig Config
    {
        get => PluginConfig.MagicPotConfig;
    }

    public override bool IsEnabled
    {
        get => Config.IsPropertyEnabled(nameof(Config.Enabled));
    }

    public override bool ShouldUpdate
    {
        get => true;
    }

    public bool IsNorthPotActive => activeNorthPotIds.Count > 0;

    public DateTime? NextSpawnUtc => lastNorthPotSpawnUtc?.AddMinutes(Config.RespawnIntervalMinutes);

    public MagicPotModule(Plugin plugin, Config config)
        : base(plugin, config)
    {
    }

    public override void Update(UpdateContext context)
    {
        if (!ZoneData.IsInNorthHorn())
        {
            activeNorthPotIds.Clear();
            return;
        }

        var activePots = Svc.Fates
            .Where(IsNorthMagicPot)
            .ToList();
        var activeIds = activePots.Select(fate => (uint)fate.FateId).ToHashSet();

        foreach (var fate in activePots)
        {
            if (activeNorthPotIds.Add((uint)fate.FateId))
            {
                lastNorthPotSpawnUtc = DateTime.UtcNow;
            }
        }

        activeNorthPotIds.IntersectWith(activeIds);
    }

    private static bool IsNorthMagicPot(IFate fate)
    {
        if (EventData.GetFate(fate.FateId).IsPot)
        {
            return true;
        }

        var name = fate.Name.ToString();
        return name.Contains("Magic Pot", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("マジックポット", StringComparison.Ordinal);
    }

    public override bool RenderMainUi(RenderContext context)
    {
        panel.Draw(this);
        return true;
    }

    public override void OnTerritoryChanged(uint id)
    {
        activeNorthPotIds.Clear();

        if (id != ZoneData.NORTHHORN)
        {
            lastNorthPotSpawnUtc = null;
        }
    }
}
