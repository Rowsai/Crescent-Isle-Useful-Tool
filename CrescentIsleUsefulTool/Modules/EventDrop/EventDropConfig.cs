using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.EventDrop;

public class EventDropConfig : ModuleConfig
{
    [Checkbox] public bool ShowDemiatmaDrops { get; set; } = true;

    [Checkbox] public bool ShowNoteDrops { get; set; } = true;

    [Checkbox] public bool ShowSoulShardDrops { get; set; } = true;
}
