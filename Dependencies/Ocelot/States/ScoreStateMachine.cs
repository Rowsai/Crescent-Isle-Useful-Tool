using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ocelot.Modules;
using Ocelot.States;

namespace Ocelot.ScoreBased;

public class ScoreStateMachine<T, M>
    where T : struct, Enum
    where M : IModule
{
    public T State { get; protected set; }

    private readonly T initial;

    public readonly Dictionary<T, ScoreStateHandler<T, M>> Handlers = [];

    protected readonly M Module;

    protected ScoreStateHandler<T, M> CurrentHandler
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

    public ScoreStateMachine(T state, M module)
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

            if (Activator.CreateInstance(type, module) is not ScoreStateHandler<T, M> instance)
            {
                throw new InvalidOperationException($"Failed to create instance of {type.FullName} with module argument.");
            }

            Logger.Debug($"Registering handler for '{State.GetType().Name}.{attr.State}'");

            Handlers[attr.State] = instance;
        }

        CurrentHandler.Enter();
    }


    public void Update()
    {
        if (!ShouldUpdate())
        {
            return;
        }

        if (!CurrentHandler.Handle())
        {
            return;
        }

        CurrentHandler.Exit();
        State = Handlers
            .OrderByDescending(handler => handler.Value.GetScore())
            .Select(handler => handler.Key)
            .First();
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
        where THandler : ScoreStateHandler<T, M>
    {
        if (CurrentHandler is THandler typed)
        {
            handler = typed;
            return true;
        }

        handler = null!;
        return false;
    }
}
