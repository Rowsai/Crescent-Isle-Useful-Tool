using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using Ocelot.Chain;

namespace CrescentIsleUsefulTool.Modules.Buff.Chains;

/// <summary>
/// Mandatory activation for たんきゅうしん (Action ID 46606).
/// It deliberately ignores the optional refresh setting and current buff
/// timers, while preserving the support job active at execution time. The
/// caller must first move to a loaded knowledge crystal.
/// </summary>
public sealed class TankyushinActivationChain : ChainFactory
{
    private Job startingJob = Job.Freelancer;

    protected override Chain Create(Chain chain)
    {
        // Never change jobs or issue the action until the movement chain has
        // actually reached a loaded knowledge-crystal object.
        chain.Then(_ => ZoneData.IsNearKnowledgeCrystal());
        chain.Then(_ => startingJob = Job.Current);
        chain.Then(Job.Freelancer.ChangeToChain);
        chain.Then(_ => Svc.Log.Info("Using たんきゅうしん (Action ID 46606) beside a knowledge crystal."));
        // Wait for the concrete action to become usable instead of issuing a
        // one-frame request that can be lost while the support job is changing.
        chain.Then(Actions.Freelancer.Tankyushin.GetCastChain()).Wait(1500);
        chain.Then(() => startingJob.ChangeToChain());
        return chain;
    }

    public override TaskManagerConfiguration Config()
    {
        return new TaskManagerConfiguration { TimeLimitMS = 30000, ShowError = false };
    }
}
