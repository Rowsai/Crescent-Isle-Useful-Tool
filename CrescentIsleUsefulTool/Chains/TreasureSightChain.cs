using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Modules.Treasure;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;

namespace CrescentIsleUsefulTool.Chains;

public class TreasureSightChain(TreasureModule module, bool force) : ChainFactory
{
    private Job startingJob = Job.Freelancer;

    protected override Chain Create(Chain chain)
    {
        chain.RunIf(() => force || module.Config.CastTreasureSightUponReturn);

        // Capture the support job when this queued chain actually starts.
        // Capturing it in the constructor can restore a stale job after a
        // preceding automation chain has changed it.
        chain.Then(_ => startingJob = Job.Current);
        chain.Then(_ => module.Tracker.BeginCountMeasurement());
        chain.Then(Job.Freelancer.ChangeToChain);
        chain.Then(_ => ECommons.DalamudServices.Svc.Log.Info("Using マギ・トレジャーサーチ (Action ID 41651) beside the base-camp aetheryte."));
        chain.Then(Actions.Freelancer.Treasuresight.GetCastChain()).Wait(1500);
        chain.Then(() => startingJob.ChangeToChain());

        return chain;
    }
}
