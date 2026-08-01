using System;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.Buff;
using CrescentIsleUsefulTool.Modules.Buff.Chains;
using CrescentIsleUsefulTool.Modules.Teleporter;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Chains;

public class ReturnChain(TeleporterModule module, ReturnChainConfig config) : RetryChainFactory
{
    private bool complete = false;

    private bool performedDemiReturn = false;

    protected override Chain Create(Chain chain)
    {
        performedDemiReturn = false;
        chain.BreakIf(() => Player.IsDead);

        var vnav = module.GetIPCSubscriber<VNavmesh>();
        var shouldReturn = false;
        var movementBlocksReturn = false;

        // Evaluate at execution time. A chain may have been queued while the
        // character was still travelling and reach this step beside camp.
        chain.Then(_ =>
        {
            shouldReturn = ShouldUseDemiReturn();
            movementBlocksReturn = shouldReturn && VnavmeshIpc.IsMovementActive(vnav);
            if (movementBlocksReturn)
            {
                Svc.Log.Info(config.WaitForStationaryDemiReturn
                    ? "Waiting for movement to stop before the required Demi-Déjion."
                    : "Skipping Demi-Déjion because movement or pathfinding is active.");
            }
        });
        chain.ConditionalThen(
            _ => shouldReturn && (!movementBlocksReturn || config.WaitForStationaryDemiReturn),
            () => CreateDemiReturnChain(vnav)
        );

        chain.Then(ChainHelper.TreasureSightChain(config.UpdateTreasureCount));
        // Every successful Demi-Déjion performs the Inquiring Mind check.
        // ApplyBuffs remains useful for callers already at camp, but can no
        // longer suppress this post-return safety check.
        chain.ConditionalThen(_ => config.ApplyBuffs || performedDemiReturn, ApplyBuffs);

        if (config.ApproachAetheryte)
        {
            var lifestream = module.GetIPCSubscriber<Lifestream>();
            var position = GetAetherytePosition();

            chain.Then(new PathfindAndMoveToChain(vnav, GetAetherytePosition()));
            chain.Then(_ =>
                LifestreamIpc.TryGetActiveCustomAetheryte(lifestream, out var active) &&
                active != 0 &&
                Player.DistanceTo(position) <= AethernetData.DISTANCE);
            chain.Then(_ => { VnavmeshIpc.TryStop(vnav); });
        }


        return chain.Then(_ => complete = true);
    }

    private Chain CreateDemiReturnChain(VNavmesh vnav)
    {
        var chain = Chain.Create("DemiReturn");
        if (config.WaitForStationaryDemiReturn)
        {
            chain.Then(_ => !VnavmeshIpc.IsMovementActive(vnav));
            chain.BreakIf(() => !ShouldUseDemiReturn());
        }
        else
        {
            chain.RunIf(() => ShouldUseDemiReturn() && !VnavmeshIpc.IsMovementActive(vnav));
        }

        chain = Actions.Return.CastOnChain(chain);
        chain.WaitToCast()
            .WaitToCycleCondition(ConditionFlag.BetweenAreas)
            .Then(_ => performedDemiReturn = true);
        return chain;
    }

    private bool ShouldUseDemiReturn()
    {
        return !ZoneData.IsNearBaseCamp() &&
               (config.ForceReturn || GetCostToReturn() < GetCostToWalk());
    }

    private Chain ApplyBuffs()
    {
        var vnav = module.GetIPCSubscriber<VNavmesh>();
        var buffs = module.GetModule<BuffModule>();
        var crystalPosition = Vector3.Zero;

        var chain = Chain.Create("Return.InquiringMindCheck");
        chain.RunIf(buffs.ShouldRefreshBuffs);
        chain.Then(new TaskManagerTask(() =>
        {
            if (!VnavmeshIpc.IsOperational(vnav, out _))
            {
                return false;
            }

            // The object table can take several frames to repopulate after
            // Demi-Déjion. Wait for a fresh object instead of checking once
            // and silently skipping the buff sequence.
            var crystal = ZoneData.GetNearbyKnowledgeCrystal(60f).FirstOrDefault();
            if (crystal == null)
            {
                return false;
            }

            crystalPosition = crystal.Position;
            return true;
        }, new TaskManagerConfiguration { TimeLimitMS = 15000, ShowError = false }));
        chain.Then(_ => Actions.TryUnmount());

        chain.Then(() => Chain.Create("Return.ApproachKnowledgeCrystal")
            .Then(new PathfindAndMoveToChain(vnav, crystalPosition))
            .WaitUntilNear(vnav, crystalPosition, AethernetData.DISTANCE)
            .Then(_ => { VnavmeshIpc.TryStop(vnav); }));

        chain.Then(new AllBuffsChain(buffs));
        chain.Then(new TaskManagerTask(
            () => !buffs.ShouldRefreshBuffs(),
            new TaskManagerConfiguration { TimeLimitMS = 15000, ShowError = false }));

        return chain;
    }

    public override bool IsComplete()
    {
        return complete;
    }

    public override int GetMaxAttempts()
    {
        return 5;
    }

    public override TaskManagerConfiguration? Config()
    {
        return new TaskManagerConfiguration { TimeLimitMS = 60000 };
    }

    private Vector3 GetAetherytePosition()
    {
        if (ZoneData.Aetherytes.TryGetValue(Svc.ClientState.TerritoryType, out var position))
        {
            return position;
        }

        throw new Exception("Unable to determine Aetheryte position");
    }

    private float GetCostToReturn()
    {
        if (ZoneData.StartingLocations.TryGetValue(Svc.ClientState.TerritoryType, out var start))
        {
            return Vector3.Distance(start, GetAetherytePosition()) + 75f;
        }


        throw new Exception("Unable to determine Starting position");
    }

    private float GetCostToWalk()
    {
        return Player.DistanceTo(GetAetherytePosition());
    }
}
