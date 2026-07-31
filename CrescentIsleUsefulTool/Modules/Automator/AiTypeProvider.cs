using Ocelot.Config.Handlers;

namespace CrescentIsleUsefulTool.Modules.Automator;

public class AiTypeProvider : EnumProvider<AiType>
{
    public override string GetLabel(AiType item)
    {
        return item.ToLabel();
    }
}
