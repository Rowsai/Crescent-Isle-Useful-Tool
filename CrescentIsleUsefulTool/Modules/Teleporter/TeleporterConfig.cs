using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Teleporter;

public class TeleporterConfig : ModuleConfig
{
    [Checkbox]
    [RequiredPlugin("Lifestream")]
    [AutomationModeCompatible]

    public bool ShouldMount { get; set; } = true;

    [Checkbox]
    [Automation]
    [RequiredPlugin("vnavmesh")]

    public bool PathToDestination { get; set; } = false;

    [Checkbox] public bool ReturnAfterFate { get; set; } = false;

    [Checkbox] public bool ReturnAfterCriticalEncounter { get; set; } = false;

    [Checkbox]
    [RequiredPlugin("vnavmesh")]

    public bool ApproachAetheryte { get; set; } = false;
}
