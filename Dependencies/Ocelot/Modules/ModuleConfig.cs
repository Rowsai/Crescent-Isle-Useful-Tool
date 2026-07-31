using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ECommons.Reflection;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Attributes;
using Ocelot.Config.Handlers;
using Ocelot.Ui;

namespace Ocelot.Modules;

[Serializable]
public abstract class ModuleConfig
{
    [IgnoreDataMember]
    public virtual string ProviderNamespace
    {
        get => GetType().Namespace ?? string.Empty;
    }

    [IgnoreDataMember] public IModule Owner { get; private set; } = null!;

    internal void SetOwner(IModule module)
    {
        Logger.Info($"Set owner to type of {module.GetType().Name}");
        Owner = module;
    }

    private List<Handler>? handlers;

    private IEnumerable<Handler> GetHandlers()
    {
        if (this.handlers != null)
        {
            return this.handlers;
        }

        var handlers = new List<Handler>();
        var props = GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var prop in props)
        {
            var configAttrs = prop.GetCustomAttributes<ConfigAttribute>(true);

            foreach (var attr in configAttrs)
            {
                Logger.Info($"Property: {prop.Name}, Attribute: {attr.GetType().Name}, Handler: {attr.GetHandler(this, attr, prop)?.GetType().Name}");
                handlers.Add(attr.GetHandler(this, attr, prop));
            }
        }

        if (props.Length == 0)
        {
            Logger.Info($"No properties found for config type: {GetType().Name}");
        }

        this.handlers = handlers;
        return handlers;
    }

    public string? GetTitle()
    {
        var title = GetType().GetCustomAttribute<TitleAttribute>() ?? new TitleAttribute();
        var key = title.TranslationKey;

        return Owner.T(key);
    }

    public List<string> GetText()
    {
        List<string> output = [];
        var texts = GetType().GetCustomAttributes<TextAttribute>().ToList();
        foreach (var text in texts)
        {
            output.Add(Owner.T(text.translation_key));
        }


        return output;
    }

    public bool Draw()
    {
        var dirty = false;
        OcelotUi.Region($"OcelotConfig##{GetType().FullName}", () =>
        {
            var title = GetTitle();
            if (title != null)
            {
                OcelotUi.Title(title);
            }

            foreach (var text in GetText())
            {
                ImGui.TextUnformatted(text);
            }

            var error = false;
            var requiredPlugin = GetType().GetCustomAttribute<RequiredPluginAttribute>();
            if (requiredPlugin != null)
            {
                List<string> missingClass = [];
                foreach (var plugin in requiredPlugin.dependencies)
                {
                    if (!DalamudReflector.TryGetDalamudPlugin(plugin, out _, false, true))
                    {
                        missingClass.Add(plugin);
                    }
                }

                if (missingClass.Count > 0)
                {
                    error = true;
                    OcelotUi.Indent(() =>
                    {
                        OcelotUi.Error("The following plugins are required for this module:");
                        ImGui.SameLine();
                        ImGui.TextUnformatted(string.Join(", ", missingClass));
                    });
                }
            }

            var conflictingPlugin = GetType().GetCustomAttribute<ConflictingPluginAttribute>();
            if (conflictingPlugin != null)
            {
                List<string> conflictingClass = [];
                foreach (var plugin in conflictingPlugin.conflicts)
                {
                    if (DalamudReflector.TryGetDalamudPlugin(plugin, out _, false, true))
                    {
                        conflictingClass.Add(plugin);
                    }
                }

                if (conflictingClass.Count > 0)
                {
                    error = true;
                    OcelotUi.Indent(() =>
                    {
                        OcelotUi.Error("The following plugins are conflicting with this module:");
                        ImGui.SameLine();
                        ImGui.TextUnformatted(string.Join(", ", conflictingClass));
                    });
                }
            }

            if (error)
            {
                return;
            }

            List<string> missingAttrs = [];
            foreach (var handler in GetHandlers())
            {
                if (handler.HasUnloadedRequiredPlugins(out var unloaded))
                {
                    foreach (var item in unloaded)
                    {
                        if (!missingAttrs.Contains(item))
                        {
                            missingAttrs.Add(item);
                        }
                    }
                }
            }

            if (missingAttrs.Count > 0)
            {
                OcelotUi.Indent(() =>
                {
                    OcelotUi.Error("Some options are hidden due to missing plugins:");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(string.Join(", ", missingAttrs));
                });
            }

            List<string> conflictingAttrs = [];
            foreach (var handler in GetHandlers())
            {
                if (handler.HasLoadedConflictingPlugins(out var loaded))
                {
                    foreach (var item in loaded)
                    {
                        if (!conflictingAttrs.Contains(item))
                        {
                            conflictingAttrs.Add(item);
                        }
                    }
                }
            }

            if (conflictingAttrs.Count > 0)
            {
                OcelotUi.Indent(() =>
                {
                    OcelotUi.Error("Some options are hidden due to conflicting plugins: ");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(string.Join(", ", conflictingAttrs));
                });
            }

            OcelotUi.Indent(16, () =>
            {
                foreach (var handler in GetHandlers())
                {
                    var (handled, changed) = handler.Render();
                    if (handled && changed)
                    {
                        dirty = true;
                    }
                }
            });
        });

        return dirty;
    }

    public bool IsPropertyEnabled(string propertyName)
    {
        var prop = GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop == null || prop.PropertyType != typeof(bool))
        {
            return false;
        }

        var dependsOn = prop.GetCustomAttribute<DependsOnAttribute>();
        if (dependsOn != null)
        {
            foreach (var dep in dependsOn.dependencies)
            {
                var depProp = GetType().GetProperty(dep, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (depProp?.PropertyType != typeof(bool))
                {
                    return false;
                }

                var value = (bool)(depProp.GetValue(this) ?? false);
                if (!value)
                {
                    return false;
                }
            }
        }

        return (bool)(prop.GetValue(this) ?? false);
    }
}
