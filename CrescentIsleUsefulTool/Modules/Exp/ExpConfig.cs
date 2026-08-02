using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Exp;

public class ExpConfig : ModuleConfig
{
    // Kept for backward-compatible configuration loading. Measurement is now
    // always enabled and no longer has a user-facing setting.
    public bool Enabled { get; set; } = true;
}
