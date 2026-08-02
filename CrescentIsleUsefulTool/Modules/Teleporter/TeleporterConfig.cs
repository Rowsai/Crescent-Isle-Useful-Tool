using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Teleporter;

public class TeleporterConfig : ModuleConfig
{
    // Post-activity return and destination travel are mandatory behavior.
    // Old persisted switches are intentionally no longer modelled here, so a
    // second setting source cannot disagree with the runtime controller.
}
