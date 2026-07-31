using System;
using System.Collections.Generic;
using System.Reflection;
using Ocelot.Modules;

namespace Ocelot.States;

public class StateMachine<T, M> : IDisposable
    where T : struct, Enum
    where M : IModule
{
    public T State { get; protected set; }

    private readonly T initial;

    public readonly Dictionary<T, StateHandler<T, M>> Handlers = [];

    protected readonly M Module;

    protected StateHandler<T, M> CurrentHandler
    {
        get
        {
            if (Handlers.TryGetValue(State, out var handler))
            {
                return handler;
            }

            throw new InvalidOperationException($"No handler found for current state '{State}'");
        }
    }

    public StateMachine(T state, M module)
    {
        State = state;
        initial = State;
        Module = module;

        foreach (var type in Registry.GetTypesForStateMachine<T, M>())
        {
            var attr = type.GetCustomAttribute<StateAttribute<T>>();
            if (attr == null)
            {
                continue;
            }

            if (Handlers.ContainsKey(attr.State))
            {
                throw new InvalidOperationException($"Duplicate state handler for state '{attr.State}' in type '{type.FullName}'.");
            }

            if (Activator.CreateInstance(type, module) is not StateHandler<T, M> instance)
            {
                throw new InvalidOperationException($"Failed to create instance of {type.FullName} with module argument.");
            }

            Logger.Debug($"Registering handler for '{State.GetType().Name}.{attr.State}'");

            Handlers[attr.State] = instance;
        }

        CurrentHandler.Enter();
    }

    public virtual void Update()
    {
        if (!ShouldUpdate())
        {
            return;
        }

        var transition = CurrentHandler.Handle();
        if (transition == null || State.Equals(transition.Value))
        {
            return;
        }

        Logger.Debug($"Updating state from '{State.GetType().Name}.{State}' to '{State.GetType().Name}.{transition.Value}'");

        CurrentHandler.Exit();
        State = transition.Value;
        CurrentHandler.Enter();
    }

    public void Reset()
    {
        State = initial;
        CurrentHandler.Enter();
    }

    protected virtual bool ShouldUpdate()
    {
        return true;
    }

    public bool TryGetCurrentHandler<THandler>(out THandler handler)
        where THandler : StateHandler<T, M>
    {
        if (CurrentHandler is THandler typed)
        {
            handler = typed;
            return true;
        }

        handler = null!;
        return false;
    }

    public virtual void Dispose()
    {
        foreach (var handler in Handlers.Values)
        {
            handler.Exit();
            if (handler is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        Handlers.Clear();
    }
}
