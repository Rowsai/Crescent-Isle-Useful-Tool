using System.Collections.Generic;
using System.Numerics;
using Ocelot.Modules;
using Ocelot.Windows;

namespace CrescentIsleUsefulTool.Modules.Treasure;

[OcelotModule(1003, 1)]
public class TreasureModule(Plugin _plugin, Config config) : Module(_plugin, config)
{
    public override TreasureConfig Config
    {
        get => PluginConfig.TreasureConfig;
    }

    public override bool ShouldInitialize
    {
        get => true;
    }

    public override bool IsEnabled
    {
        get => Config.IsPropertyEnabled(nameof(Config.Enabled));
    }

    public readonly static Vector4 Bronze = new(0.804f, 0.498f, 0.196f, 1f);

    public readonly static Vector4 Silver = new(0.753f, 0.753f, 0.753f, 1f);

    public readonly static Vector4 Unknown = new(0.6f, 0.2f, 0.8f, 1f);

    public readonly TreasureTracker Tracker = new();

    internal TreasureHunt Hunter { get; private set; } = null!;

    public List<Treasure> Treasures
    {
        get => Tracker.Treasures;
    }

    private readonly Panel panel = new();

    private readonly Radar radar = new();

    public override void PostInitialize()
    {
        Hunter = new TreasureHunt(this);
    }

    public override void Update(UpdateContext context)
    {
        Tracker.Tick(Plugin);
        Hunter.Update();
    }

    public override void Render(RenderContext context)
    {
        radar.Draw(context.ForModule(this));
    }

    public override bool RenderMainUi(RenderContext context)
    {
        panel.Draw(this);

        if (Config.ShouldEnableTreasureHunt)
        {
            Hunter.Draw(this);
        }

        return true;
    }

    public override void OnTerritoryChanged(uint id)
    {
        Hunter?.ResetForTerritoryChange();
        Tracker.ResetSession();
    }

    public override void Dispose()
    {
        base.Dispose();
        Hunter?.ResetForTerritoryChange();
        Tracker.Dispose();
    }
}
