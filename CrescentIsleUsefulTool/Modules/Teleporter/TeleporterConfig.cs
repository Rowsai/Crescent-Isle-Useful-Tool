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

    public bool PathToDestination { get; set; } = true;

    [Checkbox] public bool ReturnAfterFate { get; set; } = true;

    [Checkbox] public bool ReturnAfterCriticalEncounter { get; set; } = true;

    [Checkbox]
    [RequiredPlugin("vnavmesh")]

    public bool ApproachAetheryte { get; set; } = true;
}
