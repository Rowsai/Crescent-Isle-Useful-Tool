using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;

namespace CrescentIsleUsefulTool.Modules.Buff.Chains;

public class FreelancerBuffChain(BuffModule module) : BuffChain(Job.Freelancer, AppliedStatuses, Actions.Freelancer.InquiringMind)
{
    public static readonly PlayerStatus[] AppliedStatuses =
    [
        PlayerStatus.EnduringFortitude,
        PlayerStatus.Fleetfooted,
        PlayerStatus.RomeosBallad,
        PlayerStatus.QuickerStep,
    ];

    protected override bool ShouldRun()
    {
        return module.Config.UseInquiringMind && module.ShouldRefreshBuffs();
    }
}
