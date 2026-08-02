using System;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.StateManager;
using CrescentIsleUsefulTool.Modules.MagicPot;
using CrescentIsleUsefulTool.Modules.Treasure;
using Dalamud.Interface;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Modules.Teleporter;

public class Teleporter(TeleporterModule module)
{
    private bool completionReturnRequested;
    private bool completionReturnInProgress;
    private bool completionDemiReturnCompleted;
    private ReturnChain? activeCompletionReturnChain;
    private DateTime completionReturnStartedUtc = DateTime.MinValue;
    private DateTime nextCompletionReturnAttemptUtc = DateTime.MinValue;
    private DateTime completionLastProgressUtc = DateTime.MinValue;
    private Vector3 completionLastPosition;
    private float completionLastChainProgress;
    private string completionLastStep = "";
    private int completionReturnAttempts;
    private string completionReturnStatus = "帰還処理は待機中です。";

    public bool IsCompletionReturnPending => completionReturnRequested || completionReturnInProgress;

    public string CompletionReturnStatus => activeCompletionReturnChain?.CurrentStatus ?? completionReturnStatus;

    public void Button(Aethernet? aethernet, Vector3 destination, string name, string id, EventData ev)
    {
        if (!module.TryGetIPCSubscriber<VNavmesh>(out var vnav) || !VnavmeshIpc.IsOperational(vnav, out _))
        {
            return;
        }

        if (aethernet == null)
        {
            aethernet = ZoneData.GetClosestAethernetShard(destination);
        }

        OcelotUi.Indent(() =>
        {
            PathfindingButton(destination, name, id, ev);
            TeleportButton((Aethernet)aethernet, destination, name, id, ev);
        });
    }

    private void PathfindingButton(Vector3 destination, string name, string id, EventData ev)
    {
        if (!module.TryGetIPCSubscriber<VNavmesh>(out var vnav) || !VnavmeshIpc.IsOperational(vnav, out _))
        {
            return;
        }

        if (ImGuiEx.IconButton(FontAwesomeIcon.Running, $"{name}##{id}"))
        {
            Svc.Log.Info($"Pathfinding to {name} at {destination}");

            Plugin.Chain.Submit(() => Chain.Create("Pathfinding")
                .Then(ChainHelper.MountChain())
                .Then(new PathfindingChain(vnav!, destination, ev, 20f))
                .WaitUntilNear(vnav!, destination, 205f)
            );
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"{name}へ経路移動します");
        }

        if (!module.TryGetIPCSubscriber<Lifestream>(out var lifestream) || !LifestreamIpc.IsOperational(lifestream, out _))
        {
            return;
        }

        ImGui.SameLine();
    }

    private void TeleportButton(Aethernet aethernet, Vector3 destination, string name, string id, EventData ev)
    {
        if (!module.TryGetIPCSubscriber<Lifestream>(out var lifestream) || !LifestreamIpc.IsOperational(lifestream, out _))
        {
            return;
        }

        var isNearShards = ZoneData.GetNearbyAethernetShards().Any();
        var isNearCurrentShard = ZoneData.IsNearAethernetShard(aethernet);

        if (ImGuiEx.IconButton(FontAwesomeIcon.LocationArrow, $"{name}##{id}", enabled: isNearShards && !isNearCurrentShard))
        {
            Chain Factory()
            {
                var chain = Chain.Create("Teleport Sequence")
                    .Then(ChainHelper.TeleportChain(aethernet))
                    .Debug("Waiting for lifestream to not be 'busy'")
                    .Then(new TaskManagerTask(
                        () => LifestreamIpc.TryIsBusy(lifestream, out var isBusy) && !isBusy,
                        new TaskManagerConfiguration { TimeLimitMS = 30000 }));

                if (module.TryGetIPCSubscriber<VNavmesh>(out var vnav) && VnavmeshIpc.IsOperational(vnav, out _))
                {
                    chain.Then(ChainHelper.MountChain())
                        .Then(new PathfindingChain(vnav!, destination, ev, 20f))
                        .WaitUntilNear(vnav!, destination, 20f);
                }

                return chain;
            }

            Plugin.Chain.Submit(Factory);
        }

        if (!ImGui.IsItemHovered())
        {
            return;
        }

        if (!isNearShards)
        {
            ImGui.SetTooltip("転送するには魔導通路の近くにいる必要があります");
        }
        else if (isNearCurrentShard)
        {
            ImGui.SetTooltip("すでにこの魔導通路の近くにいます");
        }
        else
        {
            ImGui.SetTooltip($"{aethernet.ToFriendlyString()}へ転送します");
        }
    }

    public void OnFateEnd(StateManagerModule states)
    {
        RequestMandatoryCompletionReturn("FATE完了");
    }

    public void OnCriticalEncounterEnd(StateManagerModule states)
    {
        RequestMandatoryCompletionReturn("CE完了");
    }

    /// <summary>
    /// Queues the single mandatory completion sequence shared by automated and
    /// manually participated activities. Repeated event notifications are
    /// coalesced, preventing a second Demi-Déjion beside the base aetheryte.
    /// </summary>
    public bool RequestMandatoryCompletionReturn(string reason)
    {
        if (!ZoneData.IsInOccultCrescent() || ZoneData.IsInForkedTower() || Player.IsDead)
        {
            return false;
        }

        if (!completionReturnRequested)
        {
            Svc.Log.Info($"{reason}: mandatory Demi-Déjion return requested.");
            completionDemiReturnCompleted = false;
            completionReturnAttempts = 0;
            completionReturnStatus = "完了したアクティビティの移動を停止し、帰還を準備しています。";
        }

        completionReturnRequested = true;

        if (module.TryGetModule<TreasureModule>(out var treasure) && treasure?.Hunter.IsRunning == true)
        {
            treasure.Hunter.PauseForConflictingMode(
                "FATE・CE完了後の必須帰還を優先するため、トレジャーハンターを一時停止しました。");
        }

        if (IsMagicPotTreasureSearchActive())
        {
            completionReturnStatus = "マジックポットの宝箱探索完了後に、必須帰還を再開します。";
            return true;
        }

        TryStartCompletionReturn();
        return true;
    }

    public void UpdateCompletionReturn()
    {
        if (!completionReturnRequested)
        {
            completionReturnInProgress = false;
            activeCompletionReturnChain = null;
            completionReturnStatus = "帰還処理は待機中です。";
            return;
        }

        if (!ZoneData.IsInOccultCrescent() || ZoneData.IsInForkedTower() || Player.IsDead)
        {
            completionReturnRequested = false;
            completionReturnInProgress = false;
            completionReturnStatus = "現在のエリアでは帰還処理を実行できません。";
            return;
        }

        // Guidance is awarded by the pot FATE itself. Returning immediately
        // would destroy that treasure-search flow, so retain the mandatory
        // request but suspend its chain until the separate hunt is finished.
        if (IsMagicPotTreasureSearchActive())
        {
            if (completionReturnInProgress)
            {
                SuspendCompletionReturnForMagicPot();
            }
            else
            {
                completionReturnStatus = "マジックポットの宝箱探索完了後に、必須帰還を再開します。";
            }

            return;
        }

        if (completionReturnInProgress)
        {
            UpdateCompletionProgress();
            if (DateTime.UtcNow - completionLastProgressUtc > TimeSpan.FromSeconds(35))
            {
                ScheduleCompletionRetry("帰還処理が35秒進まなかったため、安全に再試行します。");
            }
        }

        // ChainQueue.Abort disposes callbacks immediately, so detect an
        // externally cancelled return here and retry instead of remaining stuck.
        if (completionReturnInProgress &&
            DateTime.UtcNow - completionReturnStartedUtc > TimeSpan.FromMilliseconds(500) &&
            !Plugin.Chain.IsRunning &&
            Plugin.Chain.QueueCount == 0)
        {
            ScheduleCompletionRetry("帰還チェーンが中断されたため再試行します。");
        }

        TryStartCompletionReturn();
    }

    private void TryStartCompletionReturn()
    {
        if (!completionReturnRequested || completionReturnInProgress || DateTime.UtcNow < nextCompletionReturnAttemptUtc)
        {
            return;
        }

        if (!module.TryGetIPCSubscriber<VNavmesh>(out var vnav) || !VnavmeshIpc.IsOperational(vnav, out _))
        {
            completionReturnStatus = "vnavmeshの準備完了後に帰還を再開します。";
            nextCompletionReturnAttemptUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        completionReturnInProgress = true;
        completionReturnAttempts++;
        completionReturnStartedUtc = DateTime.UtcNow;
        completionLastProgressUtc = DateTime.UtcNow;
        completionLastPosition = Player.Position;
        completionLastChainProgress = 0f;
        completionLastStep = "";
        completionReturnStatus = $"帰還処理を開始しています（試行 {completionReturnAttempts}）。";
        Plugin.Chain.Abort();
        VnavmeshIpc.TryCancelAllPathfinds(vnav);
        VnavmeshIpc.TryStop(vnav);
        var returnChain = new ReturnChain(module, new ReturnChainConfig
        {
            ForceReturn = true,
            AlwaysUseDemiReturn = !completionDemiReturnCompleted,
            WaitForStationaryDemiReturn = true,
            ApproachAetheryte = true,
            ApplyBuffs = true,
            UpdateTreasureCount = true,
        });
        activeCompletionReturnChain = returnChain;
        Svc.Log.Info($"Mandatory activity completion return started (attempt {completionReturnAttempts}).");
        Plugin.Chain.Submit(() => Chain.Create("MandatoryActivityCompletionReturn")
            .Then(returnChain)
            .OnComplete(() =>
            {
                completionDemiReturnCompleted |= returnChain.PerformedDemiReturn;
                completionReturnInProgress = false;
                activeCompletionReturnChain = null;
                if (returnChain.IsComplete())
                {
                    completionReturnRequested = false;
                    completionReturnAttempts = 0;
                    completionReturnStatus = "拠点への帰還が完了しました。";
                    Svc.Log.Info("Mandatory activity completion return finished.");
                }
                else
                {
                    nextCompletionReturnAttemptUtc = DateTime.UtcNow.AddSeconds(1);
                    completionReturnStatus = "帰還処理を完了できなかったため再試行します。";
                    Svc.Log.Warning("Mandatory activity completion return did not finish; retrying.");
                }
            })
            .OnFinally(() => completionReturnInProgress = false));
    }

    private void UpdateCompletionProgress()
    {
        var step = activeCompletionReturnChain?.CurrentStatus ?? "";
        var chainProgress = Plugin.Chain.CurrentChain?.Progress ?? completionLastChainProgress;
        var position = Player.Position;
        var inAreaTransition = Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] ||
                               Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];

        if (!string.Equals(step, completionLastStep, StringComparison.Ordinal) ||
            Math.Abs(chainProgress - completionLastChainProgress) >= 0.01f ||
            Vector3.Distance(position, completionLastPosition) >= 1.5f ||
            Player.IsCasting ||
            inAreaTransition)
        {
            completionLastStep = step;
            completionLastChainProgress = chainProgress;
            completionLastPosition = position;
            completionLastProgressUtc = DateTime.UtcNow;
        }
    }

    private void ScheduleCompletionRetry(string reason)
    {
        completionDemiReturnCompleted |= activeCompletionReturnChain?.PerformedDemiReturn == true;
        activeCompletionReturnChain = null;
        completionReturnInProgress = false;
        completionReturnStatus = reason;
        nextCompletionReturnAttemptUtc = DateTime.UtcNow.AddSeconds(1);

        if (module.TryGetIPCSubscriber<VNavmesh>(out var vnav))
        {
            VnavmeshIpc.TryCancelAllPathfinds(vnav);
            VnavmeshIpc.TryStop(vnav);
        }

        Plugin.Chain.Abort();
        Svc.Log.Warning(reason);
    }

    private bool IsMagicPotTreasureSearchActive()
    {
        return module.TryGetModule<MagicPotModule>(out var magicPot) &&
               magicPot?.IsTreasureSearchActive == true;
    }

    private void SuspendCompletionReturnForMagicPot()
    {
        completionDemiReturnCompleted |= activeCompletionReturnChain?.PerformedDemiReturn == true;
        activeCompletionReturnChain = null;
        completionReturnInProgress = false;
        completionReturnStatus = "マジックポットの宝箱探索完了後に、必須帰還を再開します。";
        nextCompletionReturnAttemptUtc = DateTime.UtcNow.AddSeconds(1);

        if (module.TryGetIPCSubscriber<VNavmesh>(out var vnav))
        {
            VnavmeshIpc.TryCancelAllPathfinds(vnav);
            VnavmeshIpc.TryStop(vnav);
        }

        Plugin.Chain.Abort();
        Svc.Log.Info("Mandatory completion return suspended for the active Magic Pot treasure search.");
    }

    public void Return()
    {
        if (ZoneData.IsInForkedTower())
        {
            return;
        }

        Plugin.Chain.Submit(ChainHelper.ReturnChain(new ReturnChainConfig
        {
            ForceReturn = true,
            ApproachAetheryte = true,
            ApplyBuffs = true,
        }));
    }

    public bool IsReady()
    {
        return module.TryGetIPCSubscriber<Lifestream>(out var lifestream) && LifestreamIpc.IsOperational(lifestream, out _);
    }
}
