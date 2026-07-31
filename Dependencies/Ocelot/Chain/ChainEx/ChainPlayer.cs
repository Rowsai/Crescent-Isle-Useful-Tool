using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;

namespace Ocelot.Chain.ChainEx;

public static class ChainPlayer
{
    private static TaskManagerTask WaitUntilCasting(int timeout = 5000, int interval = 50)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainAddon.WaitUntilCasting", interval))
            {
                return Svc.Objects.LocalPlayer?.IsCasting == true;
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = timeout });
    }

    public static Chain WaitUntilCasting(this Chain chain, int timeout = 5000, int interval = 50)
    {
        return chain
            .Debug("Waiting for player to start casting")
            .Then(WaitUntilCasting(timeout, interval));
    }

    private static TaskManagerTask WaitUntilNotCasting(int timeout = 5000, int interval = 50)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainAddon.WaitUntilNotCasting", interval))
            {
                return Svc.Objects.LocalPlayer?.IsCasting == false;
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = timeout });
    }

    public static Chain WaitUntilNotCasting(this Chain chain, int timeout = 5000, int interval = 50)
    {
        return chain
            .Debug("Waiting for player to stop casting")
            .Then(WaitUntilNotCasting(timeout, interval));
    }

    public static Chain WaitToCast(this Chain chain, int timeout = 5000, int interval = 50)
    {
        return chain.WaitUntilCasting(timeout, interval).WaitUntilNotCasting(timeout, interval);
    }
}
