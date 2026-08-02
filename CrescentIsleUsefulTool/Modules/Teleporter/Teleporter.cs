using System;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.StateManager;
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

    public bool IsCompletionReturnPending => completionReturnRequested || completionReturnInProgress;

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
        }

        completionReturnRequested = true;
        TryStartCompletionReturn();
        return true;
    }

    public void UpdateCompletionReturn()
    {
        if (!completionReturnRequested)
        {
            completionReturnInProgress = false;
            activeCompletionReturnChain = null;
            return;
        }

        if (!ZoneData.IsInOccultCrescent() || ZoneData.IsInForkedTower() || Player.IsDead)
        {
            completionReturnRequested = false;
            completionReturnInProgress = false;
            return;
        }

        // ChainQueue.Abort disposes callbacks immediately, so detect an
        // externally cancelled return here and retry instead of remaining stuck.
        if (completionReturnInProgress &&
            DateTime.UtcNow - completionReturnStartedUtc > TimeSpan.FromMilliseconds(500) &&
            !Plugin.Chain.IsRunning &&
            Plugin.Chain.QueueCount == 0)
        {
            completionDemiReturnCompleted |= activeCompletionReturnChain?.PerformedDemiReturn == true;
            activeCompletionReturnChain = null;
            completionReturnInProgress = false;
            nextCompletionReturnAttemptUtc = DateTime.UtcNow.AddSeconds(1);
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
            nextCompletionReturnAttemptUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        completionReturnInProgress = true;
        completionReturnStartedUtc = DateTime.UtcNow;
        Plugin.Chain.Abort();
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
                    Svc.Log.Info("Mandatory activity completion return finished.");
                }
                else
                {
                    nextCompletionReturnAttemptUtc = DateTime.UtcNow.AddSeconds(1);
                    Svc.Log.Warning("Mandatory activity completion return did not finish; retrying.");
                }
            })
            .OnFinally(() => completionReturnInProgress = false));
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
