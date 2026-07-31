using System;
using System.Collections.Generic;
using System.Linq;
using Ocelot.Modules;

namespace Ocelot.IPC;

public class IPCManager : IDisposable
{
    private readonly List<IPCSubscriber> subscribers = [];

    public IReadOnlyList<IPCSubscriber> Subscribers
    {
        get => subscribers;
    }

    private readonly List<object> providers = [];

    public IReadOnlyList<object> Providers
    {
        get => providers;
    }

    public void Initialize()
    {
        foreach (var type in Registry.GetTypesImplementing<IPCSubscriber>())
        {
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor == null || Activator.CreateInstance(type) is not IPCSubscriber instance)
            {
                continue;
            }

            Logger.Info($"Registered IPCSubscriber: {type.FullName}");
            subscribers.Add(instance);
        }

        foreach (var type in Registry.GetTypesWithAttribute<OcelotIPCAttribute>())
        {
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor == null || Activator.CreateInstance(type) is not { } instance)
            {
                continue;
            }

            Logger.Info($"Registered OcelotIPC: {type.FullName}");
            providers.Add(instance);
        }
    }

    public void AddProvider(object provider)
    {
        providers.Add(provider);
    }

    public IPCSubscriber GetSubscriber(Type type)
    {
        var subscriber = subscribers.FirstOrDefault(type.IsInstanceOfType);
        if (subscriber == null)
        {
            throw new UnableToLoadIpcSubscriberException($"IPC subscriber of type {type.Name} was not found.");
        }

        // if (!subscriber.IsReady())
        // {
        //     throw new UnableToLoadIpcSubscriberException($"IPC subscriber of type {type.Name} was not ready.");
        // }

        return subscriber;
    }

    public T GetSubscriber<T>() where T : IPCSubscriber
    {
        var subscriber = subscribers.OfType<T>().FirstOrDefault();
        if (subscriber == null)
        {
            throw new UnableToLoadIpcSubscriberException($"IPC subscriber of type {typeof(T).Name} was not found.");
        }

        // if (!subscriber.IsReady())
        // {
        //     throw new UnableToLoadIpcSubscriberException($"IPC subscriber of type {typeof(T).Name} was not ready.");
        // }

        return subscriber;
    }

    public bool TryGetSubscriber<T>(out T? subscriber) where T : IPCSubscriber
    {
        try
        {
            subscriber = GetSubscriber<T>();
            return subscriber.IsReady();
        }
        catch (UnableToLoadIpcSubscriberException ex)
        {
            Logger.Error(ex.Message);
            subscriber = null;
            return false;
        }
    }

    public void Dispose()
    {
        providers.Clear();
        subscribers.Clear();
    }
}
