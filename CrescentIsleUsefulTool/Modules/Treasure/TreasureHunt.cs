using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.Buff.Chains;
using CrescentIsleUsefulTool.Pathfinding;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace CrescentIsleUsefulTool.Modules.Treasure;

public class TreasureHunt(TreasureModule module) : Hunter(module)
{
    // North Horn's internal planmap contains 68 random-coffer placements.
    // Nine belong to subterranean spaces below this elevation; the surface
    // route deliberately excludes them.
    private const float UndergroundElevationCeiling = -10f;

    private List<TreasureData.TreasureDatum> Treasure = [];

    private readonly HashSet<uint> completedNodeIds = [];

    private readonly HashSet<uint> unreachableNodeIds = [];

    private int countRevisionAtRouteStart;

    private bool awaitingRouteStartCount;

    public int ExtractedLocationCount => Treasure.Count;

    public int ExtractedBronzeCount => Treasure.Count(item => item.Type == TreasureData.BronzeSgbId);

    public int ExtractedSilverCount => Treasure.Count(item => item.Type == TreasureData.SilverSgbId);

    public int ExcludedUndergroundLocationCount { get; private set; }

    public int CompletedLocationCount => Treasure.Count(item => completedNodeIds.Contains(item.Id));

    public int UnreachableLocationCount => Treasure.Count(item => unreachableNodeIds.Contains(item.Id));

    public int RemainingLocationCount => Math.Max(0, Treasure.Count - CompletedLocationCount - UnreachableLocationCount);

    public int? RouteStartRemainingChestCount { get; private set; }

    public int? RouteStartBronzeChestCount { get; private set; }

    public int? RouteStartSilverChestCount { get; private set; }

    public int SurfaceOpenedThisRun { get; private set; }

    public bool SurfaceCountValidationCompleted { get; private set; }

    protected override bool RebuildRouteOnResume => ZoneData.IsInNorthHorn();

    protected override bool RebuildRouteAfterUnreachable => ZoneData.IsInNorthHorn();

    protected override bool AlwaysUseDemiReturnAtRouteStart => ZoneData.IsInNorthHorn();

    protected override bool UpdateTreasureCountAtRouteStart => true;

    protected override float GetInactiveNodeConfirmationRange()
    {
        // North Horn must actually reach each extracted placement before it
        // can be counted as absent; object-table streaming is not guaranteed
        // at the configurable 75m detection range.
        return ZoneData.IsInNorthHorn() ? 6f : base.GetInactiveNodeConfirmationRange();
    }

    public void UpdateCountValidation()
    {
        if (!awaitingRouteStartCount ||
            !module.Tracker.CountInitialised ||
            module.Tracker.CountRevision <= countRevisionAtRouteStart)
        {
            return;
        }

        RouteStartRemainingChestCount = module.Tracker.RemainingChests;
        RouteStartBronzeChestCount = module.Tracker.BronzeChests;
        RouteStartSilverChestCount = module.Tracker.SilverChests;
        awaitingRouteStartCount = false;
        Svc.Log.Info($"Magi Treasuresight confirmed {RouteStartRemainingChestCount} remaining coffers at route start.");
    }

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
        var treasure = Treasure.FirstOrDefault(item => item.Id == CurrentStep.NodeId);
        if (treasure.Id == CurrentStep.NodeId)
        {
            return treasure.Position;
        }

        Svc.Log.Warning($"Treasure route node {CurrentStep.NodeId} is no longer present; skipping it safely.");
        return Player.Position;
    }

    protected override unsafe IPathfinder CreatePathfinder()
    {
        Treasure.Clear();
        ExcludedUndergroundLocationCount = 0;
        var layoutWorld = LayoutWorld.Instance();
        var layout = layoutWorld == null ? null : layoutWorld->ActiveLayout;
        if (layout == null)
        {
            Svc.Log.Warning("No active layout");
            return CreateEmptyPathfinder();
        }

        if (!layout->InstancesByType.TryGetValue(InstanceType.Treasure, out var mapPtr, false) || mapPtr.Value == null)
        {
            Svc.Log.Warning("No active treasure map");
            return CreateEmptyPathfinder();
        }

        foreach (ILayoutInstance* instance in mapPtr.Value->Values)
        {
            if (instance == null)
            {
                continue;
            }

            var transform = instance->GetTransformImpl();
            var position = transform->Translation;
            if (position.Y <= UndergroundElevationCeiling)
            {
                if (ZoneData.IsInNorthHorn())
                {
                    ExcludedUndergroundLocationCount++;
                }

                continue;
            }

            // Treasure instances are GameObjectLayoutInstance values. Use the
            // generated field instead of a version-sensitive raw +0x30 read.
            var treasureRowId = ((GameObjectLayoutInstance*)instance)->BaseId;
            if (!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Treasure>().TryGetRow(treasureRowId, out var treasureRow))
            {
                continue;
            }

            var sgbId = treasureRow.SGB.RowId;
            if (!TreasureData.IsRandomCofferType(sgbId))
            {
                continue;
            }

            // BaseId identifies the coffer type, not the placement. North Horn
            // reuses the same BaseId at many coordinates, so the layout's
            // stable InstanceKey is the route node ID.
            var nodeId = treasureRowId;
            if (ZoneData.IsInNorthHorn())
            {
                nodeId = instance->Id.InstanceKey;
                if (nodeId == 0 || Treasure.Any(item => item.Id == nodeId))
                {
                    nodeId = CreateSyntheticNodeId(Treasure.Count);
                    while (Treasure.Any(item => item.Id == nodeId))
                    {
                        nodeId++;
                    }
                }
            }

            Treasure.Add(new TreasureData.TreasureDatum(nodeId, position, sgbId));
        }

        Treasure = (ZoneData.IsInNorthHorn()
                ? Treasure
                : Treasure.GroupBy(item => item.Id).Select(group => group.First()))
            .OrderBy(item => item.Id)
            .ToList();

        Svc.Log.Info(
            $"Extracted surface treasure placements from active layout: {Treasure.Count} total " +
            $"({ExtractedBronzeCount} bronze, {ExtractedSilverCount} silver), " +
            $"{ExcludedUndergroundLocationCount} underground placements excluded.");

        if (ZoneData.IsInNorthHorn())
        {
            return new NorthHornPathfinder(Treasure, module.PluginConfig.PathfinderConfig.TeleportCost);
        }

        return new Pathfinder(Treasure, module.PluginConfig.PathfinderConfig.ReturnCost, module.PluginConfig.PathfinderConfig.TeleportCost);
    }

    protected override unsafe bool IsPathfinderDataReady()
    {
        var layoutWorld = LayoutWorld.Instance();
        var layout = layoutWorld == null ? null : layoutWorld->ActiveLayout;
        return layout != null &&
               layout->InstancesByType.TryGetValue(InstanceType.Treasure, out var mapPtr, false) &&
               mapPtr.Value != null;
    }

    protected override bool HasAvailablePathfinderNodes()
    {
        return ZoneData.IsInNorthHorn()
            ? Treasure.Any(item => !completedNodeIds.Contains(item.Id) && !unreachableNodeIds.Contains(item.Id))
            : Treasure.Count > 0;
    }

    protected override Func<Chain> GetInteractionChain(IGameObject obj)
    {
        var entityId = obj.EntityId;
        var position = obj.Position;
        var treasureType = new Treasure(obj).GetTreasureType();

        return () =>
        {
            return Chain.Create()
                .BreakIf(() => !IsCurrentCofferNear(entityId, position))
                .Then(new TaskManagerTask(() =>
                {
                    var current = GameObjectInteraction.Resolve(entityId);
                    if (current == null || !current.IsTargetable || current.IsDead)
                    {
                        return true;
                    }

                    if (!IsRandomCoffer(current) || Player.DistanceTo(current) > DISTANCE_TO_NODE_TO_USE)
                    {
                        return false;
                    }

                    if (Player.IsMoving || VnavmeshIpc.IsMovementActive(vnav))
                    {
                        return false;
                    }

                    if (!EzThrottler.Throttle("ChestInteract", 500))
                    {
                        return false;
                    }

                    if (!GameObjectInteraction.TryInteract(entityId, DISTANCE_TO_NODE_TO_USE, position))
                    {
                        return false;
                    }

                    if (module.Tracker.RecordAcquired(entityId, treasureType))
                    {
                        SurfaceOpenedThisRun++;
                    }

                    MarkCompletedLocation(position);
                    return true;
                }, new TaskManagerConfiguration { TimeLimitMS = 10000, ShowError = false }));
        };
    }

    protected override List<uint> GetValidNodes(int max)
    {
        if (ZoneData.IsInNorthHorn())
        {
            return Treasure
                .Where(item => !completedNodeIds.Contains(item.Id) && !unreachableNodeIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToList();
        }

        return TreasureData.Levels.Where(node => node.Value <= max).Select(node => node.Key).ToList();
    }

    protected override string GetRouteDescription()
    {
        return ZoneData.IsInNorthHorn()
            ? "開始時にたんきゅうしん、デミデジョン、マギ・トレジャーサーチを実行します。毎回同じ固定順で地上の全座標へ接近し、宝箱が検出されなければ次の地点へ進みます。南西高台は乱気流で出入りし、地下空洞は対象外です。"
            : base.GetRouteDescription();
    }

    protected override void OnHuntStarted(bool isResuming)
    {
        Plugin.Chain.Abort();
        Plugin.Chain.Submit(new TankyushinActivationChain());
        countRevisionAtRouteStart = module.Tracker.CountRevision;
        awaitingRouteStartCount = true;
        RouteStartRemainingChestCount = null;
        RouteStartBronzeChestCount = null;
        RouteStartSilverChestCount = null;
        SurfaceOpenedThisRun = 0;
        SurfaceCountValidationCompleted = false;
    }

    protected override bool ShouldSkipStep(PathfinderStep step)
    {
        return ZoneData.IsInNorthHorn() &&
               step.Type == PathfinderStepType.WalkToNode &&
               (completedNodeIds.Contains(step.NodeId) || unreachableNodeIds.Contains(step.NodeId));
    }

    protected override void OnStepCompleted(PathfinderStep step)
    {
        if (ZoneData.IsInNorthHorn() && step.Type == PathfinderStepType.WalkToNode)
        {
            if (unreachableNodeIds.Contains(step.NodeId))
            {
                return;
            }

            completedNodeIds.Add(step.NodeId);
        }
    }

    protected override void OnStepUnreachable(PathfinderStep step)
    {
        var targetNodeId = GetRecoveryTargetNodeId(step);
        if (ZoneData.IsInNorthHorn() && targetNodeId != 0)
        {
            unreachableNodeIds.Add(targetNodeId);
        }
    }

    protected override string GetRouteRecoveryKey(PathfinderStep step)
    {
        var targetNodeId = GetRecoveryTargetNodeId(step);
        return ZoneData.IsInNorthHorn() && targetNodeId != 0
            ? $"NorthTreasure:{targetNodeId}"
            : base.GetRouteRecoveryKey(step);
    }

    protected override void OnProgressReset()
    {
        completedNodeIds.Clear();
        unreachableNodeIds.Clear();
        Treasure.Clear();
        ExcludedUndergroundLocationCount = 0;
        awaitingRouteStartCount = false;
        RouteStartRemainingChestCount = null;
        RouteStartBronzeChestCount = null;
        RouteStartSilverChestCount = null;
        SurfaceOpenedThisRun = 0;
        SurfaceCountValidationCompleted = false;
    }

    protected override void Teardown()
    {
        SurfaceCountValidationCompleted = RouteStartRemainingChestCount.HasValue;
        base.Teardown();
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

    private static bool IsCurrentCofferNear(ulong entityId, Vector3 expectedPosition)
    {
        var current = GameObjectInteraction.Resolve(entityId);
        return current != null && IsRandomCoffer(current) && Vector3.Distance(current.Position, expectedPosition) <= 5f;
    }

    private void MarkCompletedLocation(Vector3 position)
    {
        var nearest = Treasure
            .Where(item => Vector3.Distance(item.Position, position) <= 5f)
            .OrderBy(item => Vector3.Distance(item.Position, position))
            .FirstOrDefault();
        if (nearest.Id != 0)
        {
            completedNodeIds.Add(nearest.Id);
        }
    }

    private uint GetRecoveryTargetNodeId(PathfinderStep failedStep)
    {
        if (failedStep.Type == PathfinderStepType.WalkToNode)
        {
            return failedStep.NodeId;
        }

        // A failed aethernet or turbulence segment belongs to the next chest
        // in the fixed route. Associate retries with that chest so the first
        // failure rebuilds the path and only a repeated failure skips it.
        for (var index = stepIndex + 1; index < Steps.Count; index++)
        {
            if (Steps[index].Type == PathfinderStepType.WalkToNode)
            {
                return Steps[index].NodeId;
            }
        }

        return 0;
    }

    private static uint CreateSyntheticNodeId(int index)
    {
        return 0xF0000000u + (uint)index;
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
