using System.Linq;
using CrescentIsleUsefulTool.Ipc;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using Ocelot.IPC;
using Ocelot.States;

namespace CrescentIsleUsefulTool.Modules.MobFarmer.States;

[State<FarmerPhase>(FarmerPhase.Fighting)]
public class FightingHandler(MobFarmerModule module) : FarmerPhaseHandler(module)
{
    public override FarmerPhase? Handle()
    {
        var anyInCombat = Module.Scanner.InCombat.Any();
        if (anyInCombat && EzThrottler.Throttle("Targetter"))
        {
            Svc.Targets.Target = Module.Scanner.ResolveCentroid(Module.Scanner.InCombat);
        }


        var startingPoint = Module.Farmer.StartingPoint;
        var shouldReturnHome = Module.Config.ReturnToStartInWaitingPhase && Player.DistanceTo(startingPoint) >= Module.Config.MinEuclideanDistanceToReturnHome;
        if (shouldReturnHome && !anyInCombat)
        {
            var vnav = Module.GetIPCSubscriber<VNavmesh>();
            VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
            if (!isRunning)
            {
                VnavmeshIpc.TryPathfindAndMoveTo(vnav, startingPoint, false);
            }

            return Player.DistanceTo(startingPoint) <= 2f ? FarmerPhase.Waiting : null;
        }

        if (!anyInCombat && !Svc.Condition[ConditionFlag.InCombat])
        {
            Module.Farmer.RotationPlugin.PhantomJobOff();
            return FarmerPhase.Waiting;
        }

        return null;
    }
}
