using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Buff;

public class BuffConfig : ModuleConfig
{
    [Checkbox]
    [IllegalModeCompatible]
    [Label("generic.label.enabled")]
    public bool Enabled { get; set; } = true;

    [Checkbox] [IllegalModeCompatible] public bool UseInquiringMind { get; set; } = false;

    [IntRange(0, 25)]
    [IllegalModeCompatible]
    public int ReapplyThreshold { get; set; } = 10;
}
