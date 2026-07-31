using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using ECommons.Automation.NeoTaskManager;

namespace Ocelot.Chain;

public class ChainQueue : IDisposable
{
    private readonly LinkedList<Func<Chain>> chains = [];

    private Chain? chain = null;

    private DateTime createdAt { get; } = DateTime.UtcNow;

    public bool HasRun { get; private set; } = false;

    public TimeSpan TimeAlive
    {
        get => DateTime.UtcNow - createdAt;
    }

    public int ChainsCompleted { get; private set; } = 0;

    public void Submit(Func<Chain> factory)
    {
        HasRun = true;
        lock (chains)
        {
            chains.AddLast(factory);
        }
    }

    public void Submit(Func<Chain, Chain> factory)
    {
        HasRun = true;
        lock (chains)
        {
            chains.AddLast(() => factory(Chain.Create()));
        }
    }

    public void SubmitFront(Func<Chain> factory)
    {
        HasRun = true;
        lock (chains)
        {
            chains.AddFirst(factory);
        }
    }

    public void Submit(ChainFactory factory)
    {
        Submit(factory.Factory());
    }

    public void SubmitFront(ChainFactory factory)
    {
        SubmitFront(factory.Factory());
    }

    public void Submit(TaskManagerTask task)
    {
        Submit(() => Chain.Create().Then(task));
    }

    public void SubmitFront(TaskManagerTask task)
    {
        SubmitFront(() => Chain.Create().Then(task));
    }

    public void Abort()
    {
        lock (chains)
        {
            if (chain != null)
            {
                chain.Abort();
                chain = null;
            }

            chains.Clear();
        }

        Logger.Debug("Aborted current chain and cleared the queue.");
    }

    public void Clear()
    {
        lock (chains)
        {
            chains.Clear();
        }
    }

    public Chain? CurrentChain
    {
        get => chain;
    }

    public void Tick(IFramework framework)
    {
        if (chain != null && !chain.IsComplete())
        {
            return;
        }

        if (chain?.IsComplete() == true)
        {
            chain.Dispose();
            chain = null;
            ChainsCompleted++;
        }

        lock (chains)
        {
            if (chains.Count == 0)
            {
                chain = null;
                return;
            }

            var factory = chains.First!.Value;
            chains.RemoveFirst();
            chain = factory();
        }
    }

    public bool IsRunning
    {
        get => chain != null && !chain.IsComplete();
    }

    public int QueueCount
    {
        get
        {
            lock (chains)
            {
                return chains.Count;
            }
        }
    }

    public void Dispose()
    {
        if (chain != null)
        {
            chain.Abort();
            chain.Dispose();
            chain = null;
        }

        Clear();
    }
}
