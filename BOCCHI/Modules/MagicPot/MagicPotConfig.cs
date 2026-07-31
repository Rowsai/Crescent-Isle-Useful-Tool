using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace BOCCHI.Modules.MagicPot;

public class MagicPotConfig : ModuleConfig
{
    [Checkbox]
    [Label("generic.label.enabled")]
    public bool Enabled { get; set; } = true;

    [IntRange(20, 60)]
    public int RespawnIntervalMinutes { get; set; } = 30;
}
