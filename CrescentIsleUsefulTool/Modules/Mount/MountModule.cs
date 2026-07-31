using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Mount;

[OcelotModule(1)]
public class MountModule(Plugin plugin, Config config) : Module(plugin, config)
{
    public override MountConfig Config
    {
        get => PluginConfig.MountConfig;
    }
}
