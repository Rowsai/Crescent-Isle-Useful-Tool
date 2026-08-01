using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Modules.Treasure;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;

namespace CrescentIsleUsefulTool.Chains;

public class TreasureSightChain(TreasureModule module, bool force) : ChainFactory
{
    private readonly Job StartingJob = Job.Current;

    protected override Chain Create(Chain chain)
    {
        chain.RunIf(() => force || module.Config.CastTreasureSightUponReturn);

        chain.Then(_ => module.Tracker.BeginCountMeasurement());
        chain.Then(Job.Freelancer.ChangeToChain);
        chain.Then(Actions.Freelancer.Treasuresight.GetCastChain()).Wait(1500);
        chain.Then(StartingJob.ChangeToChain);

        return chain;
    }
}
