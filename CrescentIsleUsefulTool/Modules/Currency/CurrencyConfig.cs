using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Currency;

public class CurrencyConfig : ModuleConfig
{
    [Checkbox]
    [Label("generic.label.enabled")]
    public bool Enabled { get; set; } = true;
}
