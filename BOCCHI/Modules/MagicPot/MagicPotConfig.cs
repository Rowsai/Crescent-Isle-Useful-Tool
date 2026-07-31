using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace BOCCHI.Modules.MagicPot;

public class MagicPotConfig : ModuleConfig
{
    [Checkbox]
    [Label("generic.label.enabled")]
    public bool Enabled { get; set; } = true;
}
