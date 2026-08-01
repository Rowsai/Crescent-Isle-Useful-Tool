using Ocelot.Config.Attributes;
using Ocelot.Modules;
using ExcelMount = Lumina.Excel.Sheets.Mount;

namespace CrescentIsleUsefulTool.Modules.Mount;

public class MountConfig : ModuleConfig
{
    [ExcelSheet(typeof(ExcelMount), nameof(MountProvider))]
    [Searchable]
    [AutomationModeCompatible]
    public uint Mount { get; set; } = 1;

    [Checkbox] [AutomationModeCompatible] public bool MountRoulette { get; set; } = false;
}
