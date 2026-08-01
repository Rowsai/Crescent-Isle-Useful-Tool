using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CrescentIsleUsefulTool.Ipc;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using Ocelot.IPC;
using Ocelot.States;

namespace CrescentIsleUsefulTool.Modules.MobFarmer.States;

[State<FarmerPhase>(FarmerPhase.Gathering)]
public class GatheringHandler(MobFarmerModule module) : FarmerPhaseHandler(module)
{
    private ChainQueue ChainQueue
    {
        get => ChainManager.Get("MobFarmer+Farmer");
    }

    public override FarmerPhase? Handle()
    {
        var vnav = Module.GetIPCSubscriber<VNavmesh>();

        var inCombat = Module.Scanner.InCombat.ToArray();
        var notInCombat = Module.Scanner.NotInCombat.ToArray();

        if (inCombat.Length >= Module.Config.MinimumMobsToStartFight || notInCombat.Length == 0)
        {
            VnavmeshIpc.TryStop(vnav);
            ChainQueue.Abort();
            return FarmerPhase.Stacking;
        }

        if (Svc.Targets.Target?.IsTargetingPlayer() == true)
        {
            Svc.Targets.Target = null;
            ChainQueue.Abort();
        }

        var targetSnapshot = notInCombat.First();
        Svc.Targets.Target = Module.Scanner.Resolve(targetSnapshot);

        if (ChainQueue.IsRunning || Svc.Targets.Target == null)
        {
            return null;
        }

        var target = Svc.Targets.Target;
        if (target == null || (!target.IsTargetingPlayer() && !EzThrottler.Throttle("Repath", 500)))
        {
            return null;
        }

        Task<List<Vector3>>? task = null;
        List<Vector3> path = [];
        ChainQueue.Submit(() =>
            Chain.Create()
                .Then(_ => VnavmeshIpc.TryPathfind(vnav, Player.Position, targetSnapshot.Position, false, out task))
                .Then(_ => task!.IsCompleted)
                .Then(_ => path = task!.Result)
                .BreakIf(() => path.Count <= 1)
                .Then(_ => path.RemoveAt(0))
                .Then(_ => VnavmeshIpc.TryFollowPath(vnav, path, false))
        );

        return null;
    }
}
