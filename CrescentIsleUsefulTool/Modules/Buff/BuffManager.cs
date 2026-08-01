using System.Linq;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Modules.Buff.Chains;
using ECommons.GameHelpers;
using Ocelot.Chain;

namespace CrescentIsleUsefulTool.Modules.Buff;

public class BuffManager
{
    private bool applyBuffsOnNextTick = false;

    public void QueueBuffs()
    {
        applyBuffsOnNextTick = true;
    }

    public bool IsQueued()
    {
        return applyBuffsOnNextTick;
    }

    public void Update(BuffModule module)
    {
        if (applyBuffsOnNextTick)
        {
            applyBuffsOnNextTick = false;
            ApplyBuffs(module);
        }
    }

    public void ApplyBuffs(BuffModule module)
    {
        var manager = ChainManager.Get("CrescentIsleUsefulTool##BuffManager");
        if (manager.IsRunning)
        {
            return;
        }

        manager.Submit(new AllBuffsChain(module));
    }

    public int GetLowestBuffTimer(BuffModule module)
    {
        if (!module.Config.UseInquiringMind)
        {
            return int.MaxValue;
        }

        var timers = FreelancerBuffChain.AppliedStatuses
            .Select(buff => Player.Status.Get(buff))
            .ToList();

        // Inquiring Mind applies all four 30-minute knowledge buffs. A single
        // missing buff must trigger a refresh even when the others are fresh.
        return timers.Any(status => status == null)
            ? 0
            : timers.Min(status => (int)status!.RemainingTime);
    }

    public bool ShouldRefresh(BuffModule module)
    {
        if (!module.IsEnabled || !module.Config.UseInquiringMind)
        {
            return false;
        }

        return GetLowestBuffTimer(module) <= module.Config.ReapplyThreshold * 60;
    }
}
