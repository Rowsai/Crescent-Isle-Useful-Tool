using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.Automator;
using CrescentIsleUsefulTool.Modules.StateManager;
using Dalamud.Interface;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Modules.Teleporter;

public class Teleporter(TeleporterModule module)
{
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
                .Then(new PathfindingChain(vnav!, destination, ev, 20f))
                .ConditionalThen(_ => module.Config.ShouldMount, ChainHelper.MountChain())
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
                    chain.RunIf(() => module.Config.PathToDestination)
                        .Then(new PathfindingChain(vnav!, destination, ev, 20f))
                        .ConditionalThen(_ => module.Config.ShouldMount, ChainHelper.MountChain())
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
        if (module.GetModule<AutomatorModule>().IsEnabled)
        {
            return;
        }

        if (!module.Config.ReturnAfterFate)
        {
            return;
        }

        Return();
    }

    public void OnCriticalEncounterEnd(StateManagerModule states)
    {
        if (module.GetModule<AutomatorModule>().IsEnabled)
        {
            return;
        }

        if (!module.Config.ReturnAfterCriticalEncounter)
        {
            return;
        }

        Return();
    }

    public void Return()
    {
        if (ZoneData.IsInForkedTower())
        {
            return;
        }

        Plugin.Chain.Submit(ChainHelper.ReturnChain());
    }

    public bool IsReady()
    {
        return module.TryGetIPCSubscriber<Lifestream>(out var lifestream) && LifestreamIpc.IsOperational(lifestream, out _);
    }
}
