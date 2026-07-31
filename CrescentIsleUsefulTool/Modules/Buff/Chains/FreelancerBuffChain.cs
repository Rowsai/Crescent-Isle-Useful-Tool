using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;

namespace CrescentIsleUsefulTool.Modules.Buff.Chains;

public class FreelancerBuffChain(BuffModule module) : BuffChain(Job.Freelancer, PlayerStatus.QuickerStep, Actions.Freelancer.InquiringMind)
{
    protected override bool ShouldRun()
    {
        return module.Config.UseInquiringMind;
    }
}
