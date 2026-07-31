using System;
using System.Reflection;

namespace Ocelot.Modules;

public abstract partial class Module<P, C>
    where P : OcelotPlugin
    where C : IOcelotConfig
{
    public ModuleManager Modules
    {
        get => Plugin.Modules;
    }

    public virtual void InjectModules()
    {
        var type = GetType();

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy))
        {
            if (!Attribute.IsDefined(field, typeof(InjectModuleAttribute)))
            {
                continue;
            }

            if (!typeof(IModule).IsAssignableFrom(field.FieldType))
            {
                continue;
            }

            var module = GetModule(field.FieldType);
            if (module != null)
            {
                field.SetValue(this, module);
            }
        }

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy))
        {
            if (!Attribute.IsDefined(prop, typeof(InjectModuleAttribute)))
            {
                continue;
            }

            if (!typeof(IModule).IsAssignableFrom(prop.PropertyType))
            {
                continue;
            }

            if (!prop.CanWrite)
            {
                continue;
            }

            var module = GetModule(prop.PropertyType);
            if (module != null)
            {
                prop.SetValue(this, module);
            }
        }
    }

    public T GetModule<T>() where T : class, IModule
    {
        return Modules.GetModule<T>();
    }


    public bool TryGetModule<T>(out T? module) where T : class, IModule
    {
        return Modules.TryGetModule(out module);
    }

    private IModule? GetModule(Type type)
    {
        var method = typeof(Module<P, C>).GetMethod(nameof(GetModule), []);
        var generic = method?.MakeGenericMethod(type);
        return generic?.Invoke(this, null) as IModule;
    }
}
