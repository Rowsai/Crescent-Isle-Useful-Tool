using System;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.Fates;
using CrescentIsleUsefulTool.Modules.StateManager;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Modules.Automator;

public class FateActivity(EventData data, Lifestream lifestream, VNavmesh vnav, AutomatorModule module, Fate fate)
    : Activity(data, lifestream, vnav, module)
{
    protected override TaskManagerTask GetPathfindingWatcher(StateManagerModule states)
    {
        var lastTargetPos = Vector3.Zero;

        return new TaskManagerTask(() =>
        {
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
            {
                return false;
            }

            if (EzThrottler.Throttle("FatePathfindingWatcher.EnemyScan", 100))
            {
                if (Svc.Targets.Target == null)
                {
                    var enemy = GetEnemies().Centroid();
                    if (enemy != null)
                    {
                        Svc.Targets.Target = enemy;
                    }
                }
            }

            var target = Svc.Targets.Target;
            if (target != null)
            {
                if (Vector3.Distance(target.Position, lastTargetPos) > 5f)
                {
                    if (VnavmeshIpc.TryPathfindAndMoveTo(vnav, target.Position, false))
                    {
                        lastTargetPos = target.Position;
                    }
                }

                if (states.GetState() == State.InFate)
                {
                    var distance = Vector3.Distance(Player.Position, target.Position) - target.HitboxRadius;
                    if (distance <= module.Config.EngagementRange)
                    {
                        Actions.TryUnmount();

                        VnavmeshIpc.TryStop(vnav);

                        return true;
                    }
                }
            }

            if (!VnavmeshIpc.TryIsRunning(vnav, out var isRunning) || !isRunning)
            {
                throw new VnavmeshStoppedException();
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = 180000, ShowError = false });
    }

    protected override float GetRadius()
    {
        return fate.Radius;
    }

    public override bool IsValid()
    {
        return module.GetModule<FatesModule>().fates.ContainsKey(fate.Id);
    }

    protected override Vector3 GetPosition()
    {
        return fate.StartPosition;
    }

    public override string GetName()
    {
        return EventData.GetFateDisplayName(fate.Id, fate.Name);
    }

    protected override bool IsActivityTarget(IBattleNpc obj)
    {
        return obj.IsValid() && Vector3.Distance(obj.Position, fate.StartPosition) <= fate.Radius + 15f;
    }

    protected override ActivityState GetPostPathfindingState()
    {
        return ActivityState.Participating;
    }
}
