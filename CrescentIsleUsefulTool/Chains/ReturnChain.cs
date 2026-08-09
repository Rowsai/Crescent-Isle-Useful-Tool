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

    public bool PerformedDemiReturn => performedDemiReturn;

    public string CurrentStatus { get; private set; } = "帰還処理を準備しています。";

    protected override Chain Create(Chain chain)
    {
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
            CurrentStatus = shouldReturn
                ? "デミデジョンの実行条件を確認しています。"
                : "拠点内からナレッジクリスタルとエーテライトを確認しています。";
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

        // Every successful Demi-Déjion performs the たんきゅうしん check.
        // ApplyBuffs remains useful for callers already at camp, but can no
        // longer suppress this post-return safety check.
        chain.ConditionalThen(
            _ => config.ApplyBuffs || config.ForceTankyushin || performedDemiReturn,
            () =>
            {
                CurrentStatus = "ナレッジクリスタル付近でバフを確認しています。";
                return ApplyBuffs(config.ForceTankyushin);
            });

        if (config.ApproachAetheryte)
        {
            var lifestream = module.GetIPCSubscriber<Lifestream>();
            var position = GetAetherytePosition();

            chain.Then(_ => CurrentStatus = "拠点エーテライト付近へ移動しています。");
            chain.Then(new PathfindAndMoveToChain(vnav, GetAetherytePosition()));
            chain.Then(_ =>
                LifestreamIpc.TryGetActiveCustomAetheryte(lifestream, out var active) &&
                active != 0 &&
                Player.DistanceTo(position) <= AethernetData.DISTANCE);
            chain.Then(_ => { VnavmeshIpc.TryStop(vnav); });
        }

        // Treasuresight must be issued only after the return and the final
        // approach to the base-camp aetheryte have both completed.
        chain.Then(_ => CurrentStatus = "エーテライト付近でマギ・トレジャーサーチを実行しています。");
        chain.Then(ChainHelper.TreasureSightChain(config.UpdateTreasureCount));

        return chain.Then(_ =>
        {
            CurrentStatus = "拠点への帰還が完了しました。";
            complete = true;
        });
    }

    private Chain CreateDemiReturnChain(VNavmesh vnav)
    {
        var returnRequestedAtUtc = DateTime.MinValue;
        var lastActionRequestUtc = DateTime.MinValue;
        var castFinishedAtUtc = DateTime.MinValue;
        var castStarted = false;
        var sawBetweenAreas = false;

        return Chain.Create("DemiReturn")
            .BreakIf(() => !ShouldUseDemiReturn())
            .Then(new TaskManagerTask(() =>
            {
                var betweenAreas = Svc.Condition[ConditionFlag.BetweenAreas] ||
                                   Svc.Condition[ConditionFlag.BetweenAreas51];
                if (betweenAreas)
                {
                    // The return is committed as soon as the area transition is
                    // observed. If the outer chain is suspended afterwards, a
                    // retry must not cast Demi-Déjion a second time at camp.
                    performedDemiReturn = true;
                    CurrentStatus = "デミデジョンによるエリア移動を確認しています。";
                    sawBetweenAreas = true;
                    return false;
                }

                if (sawBetweenAreas)
                {
                    performedDemiReturn = true;
                    return true;
                }

                // Demi-Déjion cannot be cast while mounted, in combat, moving,
                // or while another action is being cast. Resolve those states
                // here and keep retrying instead of timing out after one cast.
                if (Svc.Condition[ConditionFlag.InCombat])
                {
                    CurrentStatus = "戦闘中のためデミデジョン帰還を一時停止しています。";
                    // Never touch the shared vnavmesh path during combat. The
                    // configured AI provider owns movement until combat ends.
                    return false;
                }

                if (Svc.Condition[ConditionFlag.Mounted])
                {
                    CurrentStatus = "デミデジョン実行のためマウントを降りています。";
                    if (DateTime.UtcNow - lastActionRequestUtc >= TimeSpan.FromSeconds(1))
                    {
                        Actions.TryUnmount();
                        lastActionRequestUtc = DateTime.UtcNow;
                    }

                    return false;
                }

                if (VnavmeshIpc.IsMovementActive(vnav))
                {
                    CurrentStatus = "移動を停止してデミデジョンを準備しています。";
                    VnavmeshIpc.TryStop(vnav);
                    return false;
                }

                if (Player.IsCasting)
                {
                    if (returnRequestedAtUtc != DateTime.MinValue)
                    {
                        castStarted = true;
                        CurrentStatus = "デミデジョンを詠唱しています。";
                    }

                    return false;
                }

                if (castStarted)
                {
                    if (castFinishedAtUtc == DateTime.MinValue)
                    {
                        castFinishedAtUtc = DateTime.UtcNow;
                    }

                    // A cancelled cast never enters BetweenAreas. Reset after
                    // a short grace period so Demi-Déjion is requested again.
                    if (DateTime.UtcNow - castFinishedAtUtc < TimeSpan.FromSeconds(4))
                    {
                        return false;
                    }

                    castStarted = false;
                    castFinishedAtUtc = DateTime.MinValue;
                    returnRequestedAtUtc = DateTime.MinValue;
                }

                if (returnRequestedAtUtc != DateTime.MinValue &&
                    DateTime.UtcNow - returnRequestedAtUtc > TimeSpan.FromSeconds(5))
                {
                    returnRequestedAtUtc = DateTime.MinValue;
                }

                if (returnRequestedAtUtc == DateTime.MinValue &&
                    Actions.Return.CanCast() &&
                    DateTime.UtcNow - lastActionRequestUtc >= TimeSpan.FromSeconds(1))
                {
                    Actions.Return.Cast();
                    CurrentStatus = "デミデジョンを要求しました。詠唱開始を確認しています。";
                    returnRequestedAtUtc = DateTime.UtcNow;
                    lastActionRequestUtc = DateTime.UtcNow;
                    Svc.Log.Info("Demi-Déjion cast requested.");
                }

                return false;
            }, new TaskManagerConfiguration
            {
                TimeLimitMS = 120000,
                ShowError = false,
                TimeoutSilently = true,
            }));
    }

    private bool ShouldUseDemiReturn()
    {
        return config.AllowDemiReturn &&
               !performedDemiReturn &&
               (config.AlwaysUseDemiReturn ||
               !ZoneData.IsNearBaseCamp() &&
               (config.ForceReturn || GetCostToReturn() < GetCostToWalk()));
    }

    private Chain ApplyBuffs(bool forceTankyushin)
    {
        var vnav = module.GetIPCSubscriber<VNavmesh>();
        var buffs = module.GetModule<BuffModule>();
        var crystalPosition = Vector3.Zero;

        var chain = Chain.Create("Return.TankyushinCheck");
        chain.RunIf(() => forceTankyushin || buffs.ShouldRefreshBuffs());
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
        chain.Then(new TaskManagerTask(
            () => !Svc.Condition[ConditionFlag.Mounted],
            new TaskManagerConfiguration { TimeLimitMS = 5000, ShowError = false, TimeoutSilently = true }));

        chain.Then(() => Chain.Create("Return.ApproachKnowledgeCrystal")
            .Then(new PathfindAndMoveToChain(vnav, crystalPosition))
            .WaitUntilNear(vnav, crystalPosition, AethernetData.DISTANCE)
            .Then(_ => { VnavmeshIpc.TryStop(vnav); })
            .Then(_ => ZoneData.IsNearKnowledgeCrystal()));

        chain.ConditionalThen(
            _ => forceTankyushin,
            new TankyushinActivationChain());
        chain.ConditionalThen(
            _ => !forceTankyushin,
            new AllBuffsChain(buffs));
        chain.Then(new TaskManagerTask(
            () => forceTankyushin
                ? FreelancerBuffChain.AppliedStatuses.All(Player.Status.Has)
                : !buffs.ShouldRefreshBuffs(),
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

    public override int GetTimeout()
    {
        return 180000;
    }

    public override TaskManagerConfiguration? Config()
    {
        return new TaskManagerConfiguration
        {
            TimeLimitMS = 180000,
            ShowError = false,
            TimeoutSilently = true,
        };
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
