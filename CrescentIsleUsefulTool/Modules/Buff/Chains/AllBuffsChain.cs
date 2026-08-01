using CrescentIsleUsefulTool.Data;
using ECommons.Automation.NeoTaskManager;
using Ocelot.Chain;

namespace CrescentIsleUsefulTool.Modules.Buff.Chains;

public class AllBuffsChain(BuffModule module) : ChainFactory
{
    private Job startingJob = Job.Freelancer;

    protected override Chain Create(Chain chain)
    {
        chain
            .Then(_ => startingJob = Job.Current)
            .Then(new FreelancerBuffChain(module))
            // たんきゅうしん is cast as Freelancer, then return to the job
            // that was active before the buff sequence started.
            .Then(() => startingJob.ChangeToChain());

        return chain;
    }

    public override TaskManagerConfiguration Config()
    {
        return new TaskManagerConfiguration { TimeLimitMS = 60000 };
    }
}
