using Dalamud.Configuration;

namespace Ocelot;

public interface IOcelotConfig : IPluginConfiguration
{
    public void Save();
}
