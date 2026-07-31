using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CrescentIsleUsefulTool.ActionHelpers;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Pathfinding;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace CrescentIsleUsefulTool.Modules.Treasure;

public class TreasureHunt(TreasureModule module) : Hunter(module)
{
    private List<TreasureData.TreasureDatum> Treasure = [];

    public int ExtractedLocationCount => Treasure.Count;

    public int ExtractedBronzeCount => Treasure.Count(item => item.Type == TreasureData.BronzeSgbId);

    public int ExtractedSilverCount => Treasure.Count(item => item.Type == TreasureData.SilverSgbId);

    protected override IEnumerable<IGameObject> GetValidObjects()
    {
        return Svc.Objects
            .Where(o => o is
            {
                ObjectKind: ObjectKind.Treasure,
                IsDead: false,
                IsTargetable: true,
            } && o.IsValid() && IsRandomCoffer(o));
    }

    protected override Vector3 GetDestinationForCurrentStep()
    {
        return Treasure.First(t => t.Id == CurrentStep.NodeId).Position;
    }

    protected override unsafe IPathfinder CreatePathfinder()
    {
        Treasure.Clear();
        var layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null)
        {
            Svc.Log.Warning("No active layout");
            return CreateEmptyPathfinder();
        }

        if (!layout->InstancesByType.TryGetValue(InstanceType.Treasure, out var mapPtr, false))
        {
            Svc.Log.Warning("No active treasure map");
            return CreateEmptyPathfinder();
        }

        foreach (ILayoutInstance* instance in mapPtr.Value->Values)
        {
            var transform = instance->GetTransformImpl();
            var position = transform->Translation;
            if (position.Y <= -10f)
            {
                continue;
            }

            var treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
            if (!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Treasure>().TryGetRow(treasureRowId, out var treasureRow))
            {
                continue;
            }

            var sgbId = treasureRow.SGB.RowId;
            if (!TreasureData.IsRandomCofferType(sgbId))
            {
                continue;
            }

            Treasure.Add(new TreasureData.TreasureDatum(treasureRowId, position, sgbId));
        }

        Treasure = Treasure
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderBy(item => item.Id)
            .ToList();

        Svc.Log.Info($"Extracted treasure placements from active layout: {Treasure.Count} total ({ExtractedBronzeCount} bronze, {ExtractedSilverCount} silver).");

        if (ZoneData.IsInNorthHorn())
        {
            return new NorthHornPathfinder(Treasure, module.PluginConfig.PathfinderConfig.TeleportCost);
        }

        return new Pathfinder(Treasure, module.PluginConfig.PathfinderConfig.ReturnCost, module.PluginConfig.PathfinderConfig.TeleportCost);
    }

    protected override unsafe bool IsPathfinderDataReady()
    {
        var layout = LayoutWorld.Instance()->ActiveLayout;
        return layout != null && layout->InstancesByType.TryGetValue(InstanceType.Treasure, out _, false);
    }

    protected override bool HasAvailablePathfinderNodes()
    {
        return Treasure.Count > 0;
    }

    protected override Func<Chain> GetInteractionChain(IGameObject obj)
    {
        return () => Chain.Create()
            .BreakIf(() => !GetValidObjects().Any(o => Vector3.Distance(o.Position, obj.Position) <= DISTANCE_TO_NODE_TO_USE))
            .ConditionalThen(_ => Player.Mounted, _ => Actions.Unmount.Cast())
            .Wait(500)
            .Then(new TaskManagerTask(() =>
            {
                if (!EzThrottler.Throttle("ChestInteract", 250))
                {
                    return false;
                }

                if (!obj.IsValid() || !GetValidObjects().Any(o => Vector3.Distance(o.Position, obj.Position) <= DISTANCE_TO_NODE_TO_USE))
                {
                    return true;
                }

                if (Player.DistanceTo(obj) > DISTANCE_TO_NODE_TO_USE)
                {
                    return false;
                }

                unsafe
                {
                    Svc.Targets.Target = obj;
                    var gameObject = (GameObject*)(void*)obj.Address;
                    var instance = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
                    TargetSystem.Instance()->InteractWithObject(gameObject);
                    var opened = instance->Flags.HasFlag(FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags.Opened);
                    if (opened)
                    {
                        module.Tracker.RecordAcquired(obj.EntityId, new Treasure(obj).GetTreasureType());
                    }

                    return opened;
                }
            }, new TaskManagerConfiguration { TimeLimitMS = 10000, ShowError = false }));
    }

    protected override List<uint> GetValidNodes(int max)
    {
        if (ZoneData.IsInNorthHorn())
        {
            return Treasure.Select(item => item.Id).Distinct().ToList();
        }

        return TreasureData.Levels.Where(node => node.Value <= max).Select(node => node.Key).ToList();
    }

    protected override string GetRouteDescription()
    {
        return ZoneData.IsInNorthHorn()
            ? "北部ベースキャンプから開始し、内部データで検出した青銅・白銀の宝箱を近い順に巡回します。"
            : base.GetRouteDescription();
    }

    private IPathfinder CreateEmptyPathfinder()
    {
        return new EmptyTreasurePathfinder();
    }

    private static bool IsRandomCoffer(IGameObject obj)
    {
        return Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Treasure>().TryGetRow(obj.BaseId, out var row)
               && TreasureData.IsRandomCofferType(row.SGB.RowId);
    }

    private sealed class EmptyTreasurePathfinder : IPathfinder
    {
        public PathfinderState State { get; private set; } = PathfinderState.FileLoaded;

        public Task<List<PathfinderStep>> FindPath(Vector3 start, List<uint> nodes)
        {
            State = PathfinderState.PathfindingDone;
            return Task.FromResult<List<PathfinderStep>>([]);
        }
    }
}
