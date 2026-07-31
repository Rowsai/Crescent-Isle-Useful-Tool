using CrescentIsleUsefulTool.Data;
using ECommons.Automation.NeoTaskManager;
using Ocelot.Chain;

namespace CrescentIsleUsefulTool.Modules.Buff.Chains;

public class AllBuffsChain(BuffModule module) : ChainFactory
{
    private readonly Job StartingJob = Job.Current;

    protected override Chain Create(Chain chain)
    {
        chain
            .Then(new FreelancerBuffChain(module))
            // Inquiring Mind is cast as Freelancer, then return to the job
            // that was active before the buff sequence started.
            .Then(StartingJob.ChangeToChain);

        return chain;
    }

    public override TaskManagerConfiguration Config()
    {
        return new TaskManagerConfiguration { TimeLimitMS = 60000 };
    }
}
