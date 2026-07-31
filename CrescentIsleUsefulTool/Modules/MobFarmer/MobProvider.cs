using CrescentIsleUsefulTool.Data;
using Ocelot.Config.Handlers;

namespace CrescentIsleUsefulTool.Modules.MobFarmer;

public class MobProvider : EnumProvider<Mob>
{
    public override string GetLabel(Mob mob)
    {
        return MobData.GetName(mob);
    }
}
