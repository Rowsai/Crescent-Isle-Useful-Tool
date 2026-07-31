using System;
using Ocelot.Modules;

namespace Ocelot.States;

public abstract class StateHandler<T, M>(M module) : IDisposable
    where T : struct, Enum
    where M : IModule
{
    public event Action<M>? OnEnter;

    public event Action<M>? OnExit;

    protected readonly M Module = module;

    public abstract T? Handle();

    public virtual void Enter()
    {
        OnEnter?.Invoke(Module);
    }

    public virtual void Exit()
    {
        OnExit?.Invoke(Module);
    }

    public virtual void Dispose()
    {
        OnEnter = null;
        OnExit = null;
    }
}
