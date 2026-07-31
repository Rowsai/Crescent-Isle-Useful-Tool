using System;
using CrescentIsleUsefulTool.Modules.Automator;
using CrescentIsleUsefulTool.Modules.Buff;
using CrescentIsleUsefulTool.Modules.Carrots;
using CrescentIsleUsefulTool.Modules.CriticalEncounters;
using CrescentIsleUsefulTool.Modules.Currency;
using CrescentIsleUsefulTool.Modules.EventDrop;
using CrescentIsleUsefulTool.Modules.Exp;
using CrescentIsleUsefulTool.Modules.Fates;
using CrescentIsleUsefulTool.Modules.ForkedTower;
using CrescentIsleUsefulTool.Modules.MobFarmer;
using CrescentIsleUsefulTool.Modules.MagicPot;
using CrescentIsleUsefulTool.Modules.Mount;
using CrescentIsleUsefulTool.Modules.Pathfinder;
using CrescentIsleUsefulTool.Modules.StateManager;
using CrescentIsleUsefulTool.Modules.Teleporter;
using CrescentIsleUsefulTool.Modules.Treasure;
using CrescentIsleUsefulTool.Modules.WindowManager;
using ECommons.DalamudServices;
using Ocelot;

namespace CrescentIsleUsefulTool;

[Serializable]
public class Config : IOcelotConfig
{
    public int Version { get; set; } = 1;

    // Core
    public MountConfig MountConfig { get; set; } = new();

    public TeleporterConfig TeleporterConfig { get; set; } = new();

    public PathfinderConfig PathfinderConfig { get; set; } = new();

    public EventDropConfig EventDropConfig { get; set; } = new();

    public WindowManagerConfig WindowManagerConfig { get; set; } = new();

    public StateManagerConfig StateManagerConfig { get; set; } = new();

    // Functional

    public FatesConfig FatesConfig { get; set; } = new();

    public MagicPotConfig MagicPotConfig { get; set; } = new();

    public CriticalEncountersConfig CriticalEncountersConfig { get; set; } = new();

    public ForkedTowerConfig ForkedTowerConfig { get; set; } = new();

    public TreasureConfig TreasureConfig { get; set; } = new();

    public CarrotsConfig CarrotsConfig { get; set; } = new();

    public BuffConfig BuffConfig { get; set; } = new();

    // Trackers
    public CurrencyConfig CurrencyConfig { get; set; } = new();

    public ExpConfig ExpConfig { get; set; } = new();

    // Other
    public MobFarmerConfig MobFarmerConfig { get; set; } = new();

    public AutomatorConfig AutomatorConfig { get; set; } = new();

    public void Save()
    {
        Svc.PluginInterface.SavePluginConfig(this);
    }
}
