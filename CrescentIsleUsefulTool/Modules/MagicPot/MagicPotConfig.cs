using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.MagicPot;

public class MagicPotConfig : ModuleConfig
{
    [Checkbox]
    [Label("generic.label.enabled")]
    public bool Enabled { get; set; } = true;

    [Checkbox]
    [Experimental]
    [Automation]
    [RequiredPlugin("vnavmesh")]
    [DependsOn(nameof(Enabled))]
    [Label("modules.magic_pot.config.treasure_search.label")]
    public bool EnableTreasureSearchMode { get; set; } = false;

    public bool ShouldEnableTreasureSearchMode => IsPropertyEnabled(nameof(EnableTreasureSearchMode));
}
