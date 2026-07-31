using System;
using ECommons.Automation.NeoTaskManager;
using ECommons.Throttlers;

namespace Ocelot.Chain;

public abstract class RetryChainFactory : ChainFactory
{
    protected int attempt = 0;

    public override Func<Chain> Factory()
    {
        var name = GetType().Name;

        return () => Chain.Create(name)
            .Then(new TaskManagerTask(() =>
            {
                if (IsComplete())
                {
                    return true;
                }

                if (attempt >= GetMaxAttempts())
                {
                    Logger.Debug($"{name}: Max retry attempts reached.");
                    return true;
                }

                var watcher = ChainManager.Get($"{name}##watcher");
                if (!watcher.IsRunning)
                {
                    if (EzThrottler.Throttle(name, GetThrottle()))
                    {
                        attempt++;
                        Logger.Debug($"{name}: Attempt {attempt}/{GetMaxAttempts()}");
                        watcher.Submit(() => Create(Chain.Create(name)));
                    }
                }

                return false;
            }, new TaskManagerConfiguration { TimeLimitMS = GetTimeout() }));
    }

    public abstract bool IsComplete();

    public virtual int GetThrottle()
    {
        return 500;
    }

    public virtual int GetTimeout()
    {
        return 10000;
    }

    public virtual int GetMaxAttempts()
    {
        return int.MaxValue;
    }

    public override TaskManagerConfiguration? Config()
    {
        return new TaskManagerConfiguration
        {
            TimeLimitMS = int.MaxValue,
        };
    }
}
