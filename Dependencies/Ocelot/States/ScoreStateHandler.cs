using System;
using Ocelot.Modules;

namespace Ocelot.ScoreBased;

public abstract class ScoreStateHandler<T, M>(M module)
    where T : struct, Enum
    where M : IModule
{
    public event Action<M>? OnEnter;

    public event Action<M>? OnExit;

    protected readonly M Module = module;

    public abstract bool Handle();

    public abstract float GetScore();

    public virtual void Enter()
    {
        OnEnter?.Invoke(Module);
    }

    public virtual void Exit()
    {
        OnExit?.Invoke(Module);
    }
}
