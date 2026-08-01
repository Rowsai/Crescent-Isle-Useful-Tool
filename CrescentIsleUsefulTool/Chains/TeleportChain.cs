using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.Teleporter;
using Dalamud.Game.ClientState.Conditions;
using ECommons.GameHelpers;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Chains;

public class TeleportChain(Aethernet aethernet, Lifestream lifestream, TeleporterModule module) : ChainFactory
{
    protected override Chain Create(Chain chain)
    {
        var vnav = module.GetIPCSubscriber<VNavmesh>();
        var nearestPosition = ZoneData.GetNearbyAethernetShards(AethernetData.DISTANCE)
            .OrderBy(shard => Player.DistanceTo(shard.Position))
            .Select(shard => (Vector3?)shard.Position)
            .FirstOrDefault();
        if (nearestPosition == null)
        {
            return chain;
        }

        chain.Then(_ => { LifestreamIpc.TryAbort(lifestream); });

        var position = nearestPosition.Value;
        if (Player.DistanceTo(position) >= AethernetData.DISTANCE)
        {
            chain.Then(new PathfindAndMoveToChain(vnav, position));
            chain.Then(_ =>
                LifestreamIpc.TryGetActiveCustomAetheryte(lifestream, out var active) &&
                active != 0 &&
                Player.DistanceTo(position) < AethernetData.DISTANCE);
        }

        chain.Then(_ => { VnavmeshIpc.TryStop(vnav); });
        chain.Then(_ => LifestreamIpc.TryAethernetTeleport(lifestream, (uint)aethernet));
        chain.WaitToCycleCondition(ConditionFlag.BetweenAreas);
        // Mount if we should mount and not pathfind, otherwise let the pathfinder handle it
        chain.ConditionalThen(_ => module.Config is { ShouldMount: true, PathToDestination: false }, ChainHelper.MountChain());

        return chain;
    }
}
