using System.Collections.Generic;
using System.Linq;
using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using ECommons.Automation.NeoTaskManager;
using ECommons.GameHelpers;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;

namespace CrescentIsleUsefulTool.Modules.Buff.Chains;

public abstract class BuffChain(Job job, IReadOnlyCollection<PlayerStatus> buffs, Action action) : ChainFactory
{
    protected override Chain Create(Chain chain)
    {
        chain.RunIf(ShouldRun)
            .Then(_ => ZoneData.IsNearKnowledgeCrystal())
            .Then(job.ChangeToChain);

        return action
            .CastOnChain(chain)
            .Then(_ => buffs.All(Player.Status.Has))
            .Then(_ => buffs.All(buff => Player.Status.Get(buff)?.RemainingTime >= 1780));
    }

    public override TaskManagerConfiguration? Config()
    {
        return new TaskManagerConfiguration { TimeLimitMS = 15000 };
    }

    protected abstract bool ShouldRun();
}
