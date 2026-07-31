using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;

namespace CrescentIsleUsefulTool.Modules.MobFarmer.Chains;

public class BattleBellChain(MobFarmerModule module) : ChainFactory
{
    private readonly Job StartingJob = Job.Current;

    protected override Chain Create(Chain chain)
    {
        chain.BreakIf(() => Actions.Geomancer.BattleBell.GetRecastTime() >= module.Config.MaximumBattleBellWaitTime);

        chain.Then(Job.Geomancer.ChangeToChain);
        chain.Then(Actions.Geomancer.BattleBell.GetCastChain()).Wait(1000);
        chain.Then(StartingJob.ChangeToChain);

        return chain;
    }
}
