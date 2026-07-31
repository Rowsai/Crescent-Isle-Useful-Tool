using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace Ocelot.Modules;

public abstract partial class Module<P, C>
    where P : OcelotPlugin
    where C : IOcelotConfig
{
    public virtual void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
    {
    }

    public virtual void OnTerritoryChanged(uint id)
    {
    }
}
