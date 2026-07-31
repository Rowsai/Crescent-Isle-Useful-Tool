using System;
using System.Collections.Generic;
using System.Reflection;
using ECommons.DalamudServices;
using Ocelot.IPC;

namespace Ocelot.Modules;

public abstract partial class Module<P, C>
    where P : OcelotPlugin
    where C : IOcelotConfig
{
    public bool HasRequiredIPCs { get; private set; } = true;

    private List<string> _missingIPCs = [];

    public IReadOnlyList<string> MissingIPCs
    {
        get => _missingIPCs.AsReadOnly();
    }

    public IPCManager Ipc
    {
        get => Plugin.IPC;
    }


    public virtual void InjectIPCs()
    {
        HasRequiredIPCs = true;
        _missingIPCs = [];

        var type = GetType();

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy))
        {
            if (!Attribute.IsDefined(field, typeof(InjectIpcAttribute)))
            {
                continue;
            }

            if (!typeof(IPCSubscriber).IsAssignableFrom(field.FieldType))
            {
                continue;
            }

            try
            {
                var subscriber = Plugin.IPC.GetSubscriber(field.FieldType);
                field.SetValue(this, subscriber);
            }
            catch (UnableToLoadIpcSubscriberException)
            {
                if (field.GetCustomAttribute<InjectIpcAttribute>()?.Required == false)
                {
                    continue;
                }

                Svc.Log.Warning($"Ipc {field.FieldType.Name} missing for {type.Name}");
                _missingIPCs.Add(field.FieldType.Name);
                HasRequiredIPCs = false;
            }
        }

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy))
        {
            if (!Attribute.IsDefined(prop, typeof(InjectIpcAttribute)))
            {
                continue;
            }

            if (!typeof(IPCSubscriber).IsAssignableFrom(prop.PropertyType))
            {
                continue;
            }

            if (!prop.CanWrite)
            {
                continue;
            }

            try
            {
                var subscriber = Plugin.IPC.GetSubscriber(prop.PropertyType);
                prop.SetValue(this, subscriber);
            }
            catch (UnableToLoadIpcSubscriberException)
            {
                if (prop.GetCustomAttribute<InjectIpcAttribute>()?.Required == false)
                {
                    continue;
                }


                Svc.Log.Warning($"Ipc {prop.PropertyType.Name} missing for {type.Name}");
                _missingIPCs.Add(prop.PropertyType.Name);
                HasRequiredIPCs = false;
            }
        }
    }

    public T GetIPCSubscriber<T>() where T : IPCSubscriber
    {
        return Ipc.GetSubscriber<T>();
    }

    public bool TryGetIPCSubscriber<T>(out T? provider) where T : IPCSubscriber
    {
        return Ipc.TryGetSubscriber(out provider);
    }
}
