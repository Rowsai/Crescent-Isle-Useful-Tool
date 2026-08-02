using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Teleporter;

public class TeleporterConfig : ModuleConfig
{
    // Legacy values are retained only so existing configuration files can be
    // loaded. Travel and post-activity return behavior is now fixed and these
    // values are intentionally not exposed in the settings UI.
    public bool ShouldMount { get; set; } = true;
    public bool PathToDestination { get; set; } = true;
    public bool ReturnAfterFate { get; set; } = true;
    public bool ReturnAfterCriticalEncounter { get; set; } = true;
    public bool ApproachAetheryte { get; set; } = true;
}
