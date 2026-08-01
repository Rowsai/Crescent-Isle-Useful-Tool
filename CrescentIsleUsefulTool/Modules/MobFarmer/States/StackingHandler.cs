using System.Linq;
using CrescentIsleUsefulTool.Ipc;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Ocelot.IPC;
using Ocelot.States;

namespace CrescentIsleUsefulTool.Modules.MobFarmer.States;

[State<FarmerPhase>(FarmerPhase.Stacking)]
public class StackingHandler(MobFarmerModule module) : FarmerPhaseHandler(module)
{
    private bool HasRunStack = false;

    public override void Enter()
    {
        base.Enter();
        HasRunStack = false;
    }

    public override FarmerPhase? Handle()
    {
        var vnav = Module.GetIPCSubscriber<VNavmesh>();

        VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
        if (HasRunStack && !isRunning)
        {
            HasRunStack = false;
            Module.Farmer.RotationPlugin.PhantomJobOn();
            return FarmerPhase.Fighting;
        }

        var targetId = Svc.Targets.Target?.EntityId;
        var candidates = Module.Scanner.InCombat.Where(mob => mob.EntityId != targetId).ToArray();
        if (candidates.Length == 0)
        {
            return FarmerPhase.Fighting;
        }

        var furthest = candidates.OrderBy(mob => Player.DistanceTo(mob.Position)).Last();
        VnavmeshIpc.TryPathfindAndMoveTo(vnav, furthest.Position, false);
        HasRunStack = true;

        return null;
    }
}
