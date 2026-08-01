using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Ocelot.Ui;
using Ocelot.Windows;
using ECommons.DalamudServices;

namespace Ocelot.Modules;

public class ModuleManager
{
    private readonly List<IModule> modules = new();

    private readonly Dictionary<IModule, int> configOrders = new();

    private readonly Dictionary<IModule, int> mainOrders = new();

    private List<IModule> toUpdate = [];

    private List<IModule> toRender = [];

    private List<IModule> toInitialize = [];

    private readonly Dictionary<string, DateTime> lastRuntimeErrors = new();

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
        toUpdate = [];
        foreach (var module in modules)
        {
            if (TryEvaluate(module, "select for update", () =>
                    module is { ShouldUpdate: true, HasRequiredIPCs: true } && module.UpdateLimit.ShouldUpdate(module, context),
                    out var shouldUpdate) && shouldUpdate)
            {
                toUpdate.Add(module);
            }
        }

        toUpdate.ForEach(module => RunSafely(module, "pre-update", () => module.PreUpdate(context)));
    }

    public void Update(UpdateContext context)
    {
        toUpdate.ForEach(module => RunSafely(module, "update", () => module.Update(context)));
    }

    public void PostUpdate(UpdateContext context)
    {
        toUpdate.ForEach(module => RunSafely(module, "post-update", () => module.PostUpdate(context)));
    }

    public void Render(RenderContext context)
    {
        toRender = [];
        foreach (var module in modules)
        {
            if (TryEvaluate(module, "select for render", () => module is { ShouldRender: true, HasRequiredIPCs: true }, out var shouldRender) && shouldRender)
            {
                toRender.Add(module);
            }
        }

        toRender.ForEach(module => RunSafely(module, "world render", () => module.Render(context)));
    }

    public void RenderMainUi(RenderContext context)
    {
        var orderedModules = GetModulesByMainOrder().ToList();
        foreach (var module in orderedModules)
        {
            OcelotUi.Region($"OcelotMain##{module.GetType().FullName}", () =>
            {
                var rendered = false;
                RunSafely(module, "main UI render", () => rendered = module.RenderMainUi(context));
                if (rendered)
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
            RunSafely(module, "config UI render", () => module.RenderConfigUi(context));
            OcelotUi.VSpace();
            if (module != orderedModules.Last())
            {
                OcelotUi.Separator();
            }
        }
    }

    public void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
    {
        toUpdate.ForEach(module => RunSafely(module, "chat message", () => module.OnChatMessage(type, timestamp, sender, message, isHandled)));
    }

    public void OnTerritoryChanged(uint id)
    {
        toUpdate.ForEach(module => RunSafely(module, "territory change", () => module.OnTerritoryChanged(id)));
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
        modules.ForEach(module => RunSafely(module, "dispose", module.Dispose));
    }

    private void RunSafely(IModule module, string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogRuntimeError(module, operation, ex);
        }
    }

    private bool TryEvaluate(IModule module, string operation, Func<bool> evaluate, out bool result)
    {
        try
        {
            result = evaluate();
            return true;
        }
        catch (Exception ex)
        {
            result = false;
            LogRuntimeError(module, operation, ex);
            return false;
        }
    }

    private void LogRuntimeError(IModule module, string operation, Exception ex)
    {
        var key = $"{module.GetType().FullName}:{operation}";
        var now = DateTime.UtcNow;
        if (lastRuntimeErrors.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(5))
        {
            return;
        }

        lastRuntimeErrors[key] = now;
        Svc.Log.Error(ex, $"Module {module.GetType().Name} failed during {operation}; the operation was isolated.");
    }
}
