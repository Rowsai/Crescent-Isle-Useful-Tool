using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Pathfinding;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using Ocelot.Chain;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace CrescentIsleUsefulTool.Modules.Treasure;

public class TreasureHunt(TreasureModule module) : Hunter(module)
{
    // North Horn stores its underground map below the surface planmap. This
    // threshold is applied only after a layout row has been proven to be a
    // bronze/silver random coffer, so unrelated Treasure instances cannot
    // inflate the underground count.
    private const float UndergroundElevationCeiling = -10f;

    private List<TreasureData.TreasureDatum> treasure = [];

    private readonly HashSet<uint> completedNodeIds = [];

    private readonly HashSet<uint> unreachableNodeIds = [];

    private readonly HashSet<uint> unsafeNodeIds = [];

    private readonly Dictionary<uint, TreasureType> openedNodeTypes = [];

    public int ExtractedLocationCount => treasure.Count;

    public int ExtractedBronzeCount => treasure.Count(item => item.Type == TreasureData.BronzeSgbId);

    public int ExtractedSilverCount => treasure.Count(item => item.Type == TreasureData.SilverSgbId);

    public int InternalRandomCofferLocationCount { get; private set; }

    public int InternalBronzeLocationCount { get; private set; }

    public int InternalSilverLocationCount { get; private set; }

    public int ExcludedUndergroundLocationCount { get; private set; }

    public int ExcludedMagicPotLocationCount { get; private set; }

    public int CompletedLocationCount => treasure.Count(item => completedNodeIds.Contains(item.Id));

    public int UnreachableLocationCount => treasure.Count(item => unreachableNodeIds.Contains(item.Id));

    public int UnsafeLocationCount => treasure.Count(item => unsafeNodeIds.Contains(item.Id));

    public int RemainingLocationCount => Math.Max(
        0,
        treasure.Count - CompletedLocationCount - UnreachableLocationCount - UnsafeLocationCount);

    public int HunterOpenedBronzeCount => openedNodeTypes.Count(item => item.Value == TreasureType.Bronze);

    public int HunterOpenedSilverCount => openedNodeTypes.Count(item => item.Value == TreasureType.Silver);

    public int SurfaceOpenedThisRun => HunterOpenedBronzeCount + HunterOpenedSilverCount;

    public int CheckedWithoutCofferCount => Math.Max(0, CompletedLocationCount - SurfaceOpenedThisRun);

    protected override bool RebuildRouteOnResume => ZoneData.IsInNorthHorn();

    protected override bool RebuildRouteAfterUnreachable => ZoneData.IsInNorthHorn();

    protected override bool CanStartHunt =>
        !ZoneData.IsInNorthHorn() || treasure.Count == 0 || RemainingLocationCount > 0;

    protected override float GetInactiveNodeConfirmationRange()
    {
        // Every North Horn placement must be visited at interaction distance.
        // The configurable radar range is intentionally not sufficient to
        // declare an inactive/already-opened coordinate checked.
        return ZoneData.IsInNorthHorn() ? DISTANCE_TO_NODE_TO_USE + 1f : base.GetInactiveNodeConfirmationRange();
    }

    protected override TimeSpan GetInactiveNodeConfirmationDelay()
    {
        return ZoneData.IsInNorthHorn() ? TimeSpan.FromMilliseconds(750) : TimeSpan.Zero;
    }

    protected override IEnumerable<IGameObject> GetValidObjects()
    {
        return Svc.Objects.Where(obj => obj is
        {
            ObjectKind: ObjectKind.Treasure,
            IsDead: false,
            IsTargetable: true,
        } && obj.IsValid() && IsRandomCoffer(obj));
    }

    protected override Vector3 GetDestinationForCurrentStep()
    {
        var datum = treasure.FirstOrDefault(item => item.Id == CurrentStep.NodeId);
        if (datum.Id == CurrentStep.NodeId)
        {
            return datum.Position;
        }

        Svc.Log.Warning($"Treasure route node {CurrentStep.NodeId} is no longer present; skipping it safely.");
        return Player.Position;
    }

    protected override IPathfinder CreatePathfinder()
    {
        try
        {
            return CreatePathfinderFromActiveLayout();
        }
        catch (Exception exception)
        {
            treasure = [];
            ResetInternalLayoutStatistics();
            if (EzThrottler.Throttle("TreasureLayoutExtractionError", 10000))
            {
                Svc.Log.Error(exception, "Treasure layout extraction failed; keeping the hunter idle and retrying safely.");
            }

            return CreateEmptyPathfinder();
        }
    }

    private unsafe IPathfinder CreatePathfinderFromActiveLayout()
    {
        var extracted = new List<TreasureData.TreasureDatum>();
        ResetInternalLayoutStatistics();

        var layoutWorld = LayoutWorld.Instance();
        var layout = layoutWorld == null ? null : layoutWorld->ActiveLayout;
        if (layout == null)
        {
            Svc.Log.Warning("No active layout; treasure extraction will retry.");
            treasure = [];
            return CreateEmptyPathfinder();
        }

        if (!layout->InstancesByType.TryGetValue(InstanceType.Treasure, out var mapPtr, false) ||
            mapPtr.Value == null)
        {
            Svc.Log.Warning("No active Treasure layout map; treasure extraction will retry.");
            treasure = [];
            return CreateEmptyPathfinder();
        }

        foreach (ILayoutInstance* instance in mapPtr.Value->Values)
        {
            if (instance == null)
            {
                continue;
            }

            var transform = instance->GetTransformImpl();
            if (transform == null)
            {
                continue;
            }

            var position = transform->Translation;
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                continue;
            }

            // InstanceType.Treasure entries are GameObjectLayoutInstance
            // values. BaseId is an Excel Treasure row, never a route ID.
            var treasureRowId = ((TreasureLayoutInstance*)instance)->BaseId;
            if (TreasureData.IsMagicPotCofferBaseId(treasureRowId))
            {
                ExcludedMagicPotLocationCount++;
                continue;
            }

            if (!Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Treasure>()
                    .TryGetRow(treasureRowId, out var treasureRow))
            {
                continue;
            }

            var sgbId = treasureRow.SGB.RowId;
            if (!TreasureData.IsRandomCofferType(sgbId))
            {
                continue;
            }

            InternalRandomCofferLocationCount++;
            if (sgbId == TreasureData.BronzeSgbId)
            {
                InternalBronzeLocationCount++;
            }
            else
            {
                InternalSilverLocationCount++;
            }

            if (ZoneData.IsInNorthHorn() && position.Y <= UndergroundElevationCeiling)
            {
                ExcludedUndergroundLocationCount++;
                continue;
            }

            // North Horn reuses the same Treasure row at many placements. The
            // layout InstanceKey identifies each coordinate. A synthetic ID is
            // used only when malformed or duplicated internal data is observed.
            var nodeId = treasureRowId;
            if (ZoneData.IsInNorthHorn())
            {
                nodeId = instance->Id.InstanceKey;
                if (nodeId == 0 || extracted.Any(item => item.Id == nodeId))
                {
                    nodeId = CreateSyntheticNodeId(extracted.Count);
                    while (extracted.Any(item => item.Id == nodeId))
                    {
                        nodeId++;
                    }
                }
            }

            extracted.Add(new TreasureData.TreasureDatum(nodeId, position, sgbId));
        }

        treasure = (ZoneData.IsInNorthHorn()
                ? extracted
                : extracted.GroupBy(item => item.Id).Select(group => group.First()))
            .OrderBy(item => item.Id)
            .ToList();

        Svc.Log.Info(
            $"Validated internal random-coffer placements: {InternalRandomCofferLocationCount} total " +
            $"({InternalBronzeLocationCount} bronze, {InternalSilverLocationCount} silver); " +
            $"surface route {ExtractedLocationCount} ({ExtractedBronzeCount} bronze, {ExtractedSilverCount} silver), " +
            $"{ExcludedUndergroundLocationCount} actual underground random-coffer placements excluded, " +
            $"{ExcludedMagicPotLocationCount} magic-pot placements excluded.");

        if (ZoneData.IsInNorthHorn())
        {
            return new NorthHornPathfinder(treasure, module.PluginConfig.PathfinderConfig.TeleportCost);
        }

        return new Pathfinder(
            treasure,
            module.PluginConfig.PathfinderConfig.ReturnCost,
            module.PluginConfig.PathfinderConfig.TeleportCost);
    }

    protected override bool IsPathfinderDataReady()
    {
        try
        {
            return IsPathfinderDataReadyUnsafe();
        }
        catch (Exception exception)
        {
            if (EzThrottler.Throttle("TreasureLayoutReadinessError", 10000))
            {
                Svc.Log.Error(exception, "Treasure layout readiness check failed; retrying safely.");
            }

            return false;
        }
    }

    private static unsafe bool IsPathfinderDataReadyUnsafe()
    {
        var layoutWorld = LayoutWorld.Instance();
        var layout = layoutWorld == null ? null : layoutWorld->ActiveLayout;
        return layout != null &&
               layout->InstancesByType.TryGetValue(InstanceType.Treasure, out var mapPtr, false) &&
               mapPtr.Value != null;
    }

    protected override bool HasAvailablePathfinderNodes()
    {
        // Let an empty remaining-node set build an empty route and teardown
        // cleanly instead of waiting forever after every location is excluded.
        return treasure.Count > 0;
    }

    protected override Func<Chain> GetInteractionChain(IGameObject obj)
    {
        var entityId = obj.EntityId;
        var position = obj.Position;
        var treasureType = new Treasure(obj).GetTreasureType();

        return () =>
        {
            var interactionStartedAtUtc = DateTime.MinValue;
            var interactionAttempts = 0;
            var interactionSent = false;

            return Chain.Create("Treasure.OpenRandomCoffer")
                .Then(new TaskManagerTask(() =>
                {
                    var current = GameObjectInteraction.Resolve(entityId);
                    if (interactionSent && !IsCurrentCofferNear(entityId, position))
                    {
                        ConfirmOpenedCoffer(position, treasureType);
                        return true;
                    }

                    // Another player can open the object between detection and
                    // this task. Record the coordinate as checked, but do not
                    // report it as acquired by this hunter.
                    if (current == null || !current.IsTargetable || current.IsDead || !IsRandomCoffer(current))
                    {
                        MarkCompletedLocation(position);
                        return true;
                    }

                    if (interactionStartedAtUtc == DateTime.MinValue)
                    {
                        interactionStartedAtUtc = DateTime.UtcNow;
                    }

                    if (DateTime.UtcNow - interactionStartedAtUtc > TimeSpan.FromSeconds(7))
                    {
                        Svc.Log.Warning(
                            $"Coffer interaction was not confirmed after {interactionAttempts} attempts; retrying the same coffer.");
                        runtimeStatus = "宝箱の開封を確認できなかったため、同じ宝箱を再試行します。";
                        return true;
                    }

                    if (Player.DistanceTo(current) > DISTANCE_TO_NODE_TO_USE ||
                        Player.IsMoving ||
                        VnavmeshIpc.IsMovementActive(vnav) ||
                        !EzThrottler.Throttle($"ChestInteract.{entityId}", 750))
                    {
                        return false;
                    }

                    if (GameObjectInteraction.TryInteract(entityId, DISTANCE_TO_NODE_TO_USE, position))
                    {
                        interactionAttempts++;
                        interactionSent = true;
                    }

                    return false;
                }, new TaskManagerConfiguration
                {
                    TimeLimitMS = 8000,
                    ShowError = false,
                    TimeoutSilently = true,
                }));
        };
    }

    protected override List<uint> GetValidNodes(int max)
    {
        if (ZoneData.IsInNorthHorn())
        {
            return treasure
                .Where(item => !IsExcludedOrCompleted(item.Id))
                .Select(item => item.Id)
                .ToList();
        }

        return TreasureData.Levels.Where(node => node.Value <= max).Select(node => node.Key).ToList();
    }

    protected override string GetRouteDescription()
    {
        return ZoneData.IsInNorthHorn()
            ? "開始時に拠点へ移動し、ナレッジクリスタル付近でたんきゅうしんを使用後、エーテライト付近でマギ・トレジャーサーチを実行します。内部データの地上青銅・白銀座標を固定順で検知距離まで巡回し、開封後は帰還せず次へ進みます。地下空洞、マジックポット宝箱、設定上限を超える敵が付近にいる地点は対象外です。"
            : base.GetRouteDescription();
    }

    protected override void OnHuntStarted(bool isResuming)
    {
        Plugin.Chain.Abort();
        Plugin.Chain.Submit(ChainHelper.TankyushinAtKnowledgeCrystalChain(updateTreasureCount: true));

        if (!isResuming && completedNodeIds.Count == 0)
        {
            openedNodeTypes.Clear();
        }
    }

    protected override bool ShouldSkipStep(PathfinderStep step)
    {
        return ZoneData.IsInNorthHorn() &&
               step.Type == PathfinderStepType.WalkToNode &&
               IsExcludedOrCompleted(step.NodeId);
    }

    protected override void OnStepCompleted(PathfinderStep step)
    {
        if (ZoneData.IsInNorthHorn() &&
            step.Type == PathfinderStepType.WalkToNode &&
            !unreachableNodeIds.Contains(step.NodeId) &&
            !unsafeNodeIds.Contains(step.NodeId))
        {
            completedNodeIds.Add(step.NodeId);
        }
    }

    protected override void OnStepUnreachable(PathfinderStep step)
    {
        var targetNodeId = GetRecoveryTargetNodeId(step);
        if (!ZoneData.IsInNorthHorn() || targetNodeId == 0)
        {
            return;
        }

        if (CurrentStepFailureReason == StepFailureReason.UnsafeEnemy)
        {
            unsafeNodeIds.Add(targetNodeId);
        }
        else
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
        unsafeNodeIds.Clear();
        openedNodeTypes.Clear();
        treasure.Clear();
        ResetInternalLayoutStatistics();
    }

    private IPathfinder CreateEmptyPathfinder()
    {
        return new EmptyTreasurePathfinder();
    }

    private static bool IsRandomCoffer(IGameObject obj)
    {
        if (TreasureData.IsMagicPotCofferBaseId(obj.BaseId))
        {
            return false;
        }

        return Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Treasure>().TryGetRow(obj.BaseId, out var row) &&
               TreasureData.IsRandomCofferType(row.SGB.RowId);
    }

    private static bool IsCurrentCofferNear(ulong entityId, Vector3 expectedPosition)
    {
        var current = GameObjectInteraction.Resolve(entityId);
        return current != null &&
               IsRandomCoffer(current) &&
               Vector3.Distance(current.Position, expectedPosition) <= 5f;
    }

    private void ConfirmOpenedCoffer(Vector3 position, TreasureType treasureType)
    {
        var nodeId = FindNearestNodeId(position);
        if (nodeId == 0 || !openedNodeTypes.TryAdd(nodeId, treasureType))
        {
            return;
        }

        completedNodeIds.Add(nodeId);
        Svc.Log.Info(
            $"Confirmed normal coffer opened at route node {nodeId} ({treasureType}); continuing to the next coordinate.");
    }

    private void MarkCompletedLocation(Vector3 position)
    {
        var nodeId = FindNearestNodeId(position);
        if (nodeId != 0)
        {
            completedNodeIds.Add(nodeId);
        }
    }

    private uint FindNearestNodeId(Vector3 position)
    {
        return treasure
            .Where(item => Vector3.Distance(item.Position, position) <= 5f)
            .OrderBy(item => Vector3.Distance(item.Position, position))
            .Select(item => item.Id)
            .FirstOrDefault();
    }

    private bool IsExcludedOrCompleted(uint nodeId)
    {
        return completedNodeIds.Contains(nodeId) ||
               unreachableNodeIds.Contains(nodeId) ||
               unsafeNodeIds.Contains(nodeId);
    }

    private uint GetRecoveryTargetNodeId(PathfinderStep failedStep)
    {
        if (failedStep.Type == PathfinderStepType.WalkToNode)
        {
            return failedStep.NodeId;
        }

        // Travel steps belong to the next coffer in the fixed route. Binding
        // recovery to that coffer prevents an aethernet/turbulence failure from
        // rebuilding forever without identifying the affected destination.
        for (var index = stepIndex + 1; index < Steps.Count; index++)
        {
            if (Steps[index].Type == PathfinderStepType.WalkToNode)
            {
                return Steps[index].NodeId;
            }
        }

        return 0;
    }

    private void ResetInternalLayoutStatistics()
    {
        InternalRandomCofferLocationCount = 0;
        InternalBronzeLocationCount = 0;
        InternalSilverLocationCount = 0;
        ExcludedUndergroundLocationCount = 0;
        ExcludedMagicPotLocationCount = 0;
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
