using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules;

using BaseModule = Module<Plugin, Config>;

public abstract class Module(Plugin _plugin, Config _config) : BaseModule(_plugin, _config);
