using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Ocelot.Ui;
using Ocelot.Windows;

namespace Ocelot.Modules;

public class ModuleManager
{
    private readonly List<IModule> modules = new();

    private readonly Dictionary<IModule, int> configOrders = new();

    private readonly Dictionary<IModule, int> mainOrders = new();

    private List<IModule> toUpdate = [];

    private List<IModule> toRender = [];

    private List<IModule> toInitialize = [];

    public void Add(Module<OcelotPlugin, IOcelotConfig> module)
    {
        modules.Add(module);
    }

    public void AutoRegister(OcelotPlugin plugin, IOcelotConfig config)
    {
        var moduleTypes = Registry
            .GetTypesWithAttributeData<OcelotModuleAttribute>()
            .Where(t => typeof(IModule).IsAssignableFrom(t.type));

        foreach (var (type, attr) in moduleTypes)
        {
            Logger.Info($"Registering module: {type.FullName}");
            var moduleInstance = (IModule)Activator.CreateInstance(type, plugin, config)!;
            modules.Add(moduleInstance);
            if (attr != null)
            {
                configOrders[moduleInstance] = attr.ConfigOrder;
                mainOrders[moduleInstance] = attr.MainOrder;
            }
        }
    }

    public IEnumerable<IModule> GetModulesByMainOrder()
    {
        return toRender.OrderBy(m => mainOrders.GetValueOrDefault(m, int.MaxValue));
    }

    public IEnumerable<IModule> GetModulesByConfigOrder()
    {
        return modules.OrderBy(m => configOrders.GetValueOrDefault(m, int.MaxValue));
    }

    public void PreInitialize()
    {
        toInitialize = modules.Where(m => m.ShouldInitialize).ToList();

        modules.ForEach(m => m.Config?.SetOwner(m));
        toInitialize.ForEach(m => m.PreInitialize());
    }

    public void Initialize()
    {
        toInitialize.ForEach(m => m.Initialize());
    }

    public void PostInitialize()
    {
        toInitialize.ForEach(m => m.PostInitialize());
    }

    public void InjectModules()
    {
        toInitialize.ForEach(m => m.InjectModules());
    }

    public void InjectIPCs()
    {
        toInitialize.ForEach(m => m.InjectIPCs());
    }

    public void PreUpdate(UpdateContext context)
    {
        toUpdate = modules.Where(m => m is { ShouldUpdate: true, HasRequiredIPCs: true } && m.UpdateLimit.ShouldUpdate(m, context)).ToList();
        toUpdate.ForEach(m => m.PreUpdate(context));
    }

    public void Update(UpdateContext context)
    {
        toUpdate.ForEach(m => m.Update(context));
    }

    public void PostUpdate(UpdateContext context)
    {
        toUpdate.ForEach(m => m.PostUpdate(context));
    }

    public void Render(RenderContext context)
    {
        toRender = modules.Where(m => m is { ShouldRender: true, HasRequiredIPCs: true }).ToList();
        toRender.ForEach(m => m.Render(context));
    }

    public void RenderMainUi(RenderContext context)
    {
        var orderedModules = GetModulesByMainOrder().ToList();
        foreach (var module in orderedModules)
        {
            OcelotUi.Region($"OcelotMain##{module.GetType().FullName}", () =>
            {
                if (module.RenderMainUi(context))
                {
                    OcelotUi.VSpace();
                    if (module != orderedModules.Last())
                    {
                        OcelotUi.Separator();
                    }
                }
            });
        }
    }

    public void RenderConfigUi(RenderContext context)
    {
        var orderedModules = GetModulesByConfigOrder().ToList();
        foreach (var module in orderedModules)
        {
            module.RenderConfigUi(context);
            OcelotUi.VSpace();
            if (module != orderedModules.Last())
            {
                OcelotUi.Separator();
            }
        }
    }

    public void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
    {
        toUpdate.ForEach(m => m.OnChatMessage(type, timestamp, sender, message, isHandled));
    }

    public void OnTerritoryChanged(uint id)
    {
        toUpdate.ForEach(m => m.OnTerritoryChanged(id));
    }

    public T GetModule<T>() where T : class, IModule
    {
        var module = modules.OfType<T>().FirstOrDefault();
        if (module == null)
        {
            throw new UnableToLoadModuleException($"Module of type {typeof(T).Name} was not found.");
        }

        return module;
    }

    public bool TryGetModule<T>(out T? module) where T : class, IModule
    {
        try
        {
            module = GetModule<T>();
            return true;
        }
        catch (UnableToLoadModuleException ex)
        {
            Logger.Error(ex.Message);
            module = null;
            return false;
        }
    }

    public void Dispose()
    {
        modules.ForEach(m => m.Dispose());
    }
}
