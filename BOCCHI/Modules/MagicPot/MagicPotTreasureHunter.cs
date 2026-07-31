using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BOCCHI.ActionHelpers;
using BOCCHI.Data;
using BOCCHI.Ipc;
using BOCCHI.Modules.Automator;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Ocelot.IPC;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace BOCCHI.Modules.MagicPot;

internal sealed class MagicPotTreasureHunter(MagicPotModule module)
{
    private const uint GuidanceStatusId = 1531;
    private const uint MagicalElixirEventItemId = 2003296;
    private const float ArrivalDistance = 2.5f;
    private const float CofferDetectionDistance = 12f;

    private static readonly HashSet<uint> MagicPotCofferBaseIds =
    [
        2009530, // Gold
        2009531, // Silver
        2009532, // Bronze
    ];

    private readonly List<HintSample> hints = [];

    private VNavmesh? vnav;
    private Vector3? target;
    private bool hadGuidance;
    private bool awaitingHint;
    private bool secondChance;
    private uint? sourcePotFateId;
    private DateTime nextElixirUseUtc = DateTime.MinValue;
    private DateTime hintDeadlineUtc = DateTime.MinValue;
    private DateTime nextMovementRetryUtc = DateTime.MinValue;

    internal bool HasGuidanceBuff => Svc.Objects.LocalPlayer?.StatusList.Any(status => status.StatusId == GuidanceStatusId) == true;

    internal bool IsActive => module.Config.ShouldEnableTreasureSearchMode && ZoneData.IsInNorthHorn() && HasGuidanceBuff;

    internal string RuntimeStatus { get; private set; } = "財宝誘導バフを待っています。";

    internal Vector3? Target => target;

    internal int HintCount => hints.Count;

    internal void Update()
    {
        var hasGuidance = HasGuidanceBuff;
        if (!module.Config.ShouldEnableTreasureSearchMode || !ZoneData.IsInNorthHorn())
        {
            if (hadGuidance)
            {
                StopAndReset("財宝探索モードは停止中です。");
            }

            return;
        }

        if (!hasGuidance)
        {
            if (hadGuidance)
            {
                StopAndReset("財宝誘導が終了しました。");
            }
            else
            {
                RuntimeStatus = "財宝誘導バフを待っています。";
            }

            return;
        }

        if (!hadGuidance)
        {
            BeginHunt();
        }

        if (!module.TryGetIPCSubscriber<VNavmesh>(out vnav) || vnav == null)
        {
            RuntimeStatus = "vnavmeshプラグインの起動を待っています。";
            return;
        }

        if (!VnavmeshIpc.IsOperational(vnav, out var waitingReason))
        {
            RuntimeStatus = waitingReason;
            return;
        }

        if (awaitingHint && DateTime.UtcNow >= hintDeadlineUtc)
        {
            // Chat delivery can be missed while loading or changing UI state.
            // Release the wait and safely retry the key item instead of stalling.
            awaitingHint = false;
        }

        var coffer = FindRevealedCoffer();
        if (coffer != null)
        {
            MoveToAndOpen(coffer);
            return;
        }

        if (target is not { } destination)
        {
            RuntimeStatus = awaitingHint ? "マジカルエリクサーの方角情報を待っています。" : "マジカルエリクサーを使用します。";
            if (!awaitingHint)
            {
                TryUseMagicalElixir();
            }

            return;
        }

        var distance = HorizontalDistance(Player.Position, destination);
        if (distance <= ArrivalDistance)
        {
            VnavmeshIpc.TryStop(vnav);
            RuntimeStatus = "推定地点に到着しました。マジカルエリクサーで確認します。";
            if (!awaitingHint)
            {
                TryUseMagicalElixir();
            }

            return;
        }

        RuntimeStatus = $"推定した宝箱地点へ移動中です（残り {distance:F0}m）。";
        if (!Player.Mounted && distance > 20f && EzThrottler.Throttle("MagicPotTreasure.Mount", 3000))
        {
            Actions.MountRoulette.Cast();
        }

        if (DateTime.UtcNow >= nextMovementRetryUtc)
        {
            VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
            if (!isRunning)
            {
                VnavmeshIpc.TryPathfindAndMoveTo(vnav, destination, false);
            }

            nextMovementRetryUtc = DateTime.UtcNow.AddSeconds(2);
        }
    }

    internal void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled)
    {
        if (!ZoneData.IsInNorthHorn() || !module.Config.ShouldEnableTreasureSearchMode)
        {
            return;
        }

        var text = message.TextValue;
        if (IsSecondChanceMessage(text))
        {
            secondChance = true;
            hints.Clear();
            target = null;
            awaitingHint = false;
            hintDeadlineUtc = DateTime.MinValue;
            nextElixirUseUtc = DateTime.UtcNow.AddSeconds(1);
            RuntimeStatus = "2つ目の黄金宝箱を探索します。";
            return;
        }

        if (IsCofferDiscoveredMessage(text))
        {
            awaitingHint = false;
            RuntimeStatus = "宝箱を発見しました。開封します。";
            return;
        }

        if (!TryParseHint(text, out var direction, out var range))
        {
            return;
        }

        awaitingHint = false;
        hintDeadlineUtc = DateTime.MinValue;
        hints.Add(new HintSample(Player.Position, direction, range));
        target = InferTarget();
        nextMovementRetryUtc = DateTime.MinValue;

        if (target is { } inferred)
        {
            var distance = HorizontalDistance(Player.Position, inferred);
            RuntimeStatus = $"ヒント{hints.Count}件から候補地点を推定しました（約 {distance:F0}m）。";
            VnavmeshIpc.TryStop(vnav);
        }
        else
        {
            RuntimeStatus = "方角情報を取得しましたが候補地点を特定できませんでした。";
        }
    }

    internal void ResetForTerritoryChange()
    {
        StopAndReset("財宝誘導バフを待っています。");
    }

    private void BeginHunt()
    {
        hadGuidance = true;
        awaitingHint = false;
        hintDeadlineUtc = DateTime.MinValue;
        secondChance = false;
        hints.Clear();
        target = null;
        sourcePotFateId = module.MostRecentPotFateId ?? InferSourcePotFromPosition();
        nextElixirUseUtc = DateTime.UtcNow;
        nextMovementRetryUtc = DateTime.MinValue;
        RuntimeStatus = "財宝誘導を検知しました。既存の帰還処理を中断します。";

        Plugin.Chain.Abort();
        if (module.TryGetIPCSubscriber<VNavmesh>(out vnav))
        {
            VnavmeshIpc.TryStop(vnav);
        }

        if (module.TryGetModule<AutomatorModule>(out var automator) && automator?.Config.Enabled == true)
        {
            automator.automator.SetRuntimeStatus("マジックポットの宝箱探索を優先しています。");
        }
    }

    private void StopAndReset(string status)
    {
        if (vnav != null)
        {
            VnavmeshIpc.TryStop(vnav);
        }

        hadGuidance = false;
        awaitingHint = false;
        hintDeadlineUtc = DateTime.MinValue;
        secondChance = false;
        hints.Clear();
        target = null;
        sourcePotFateId = null;
        RuntimeStatus = status;
    }

    private unsafe void TryUseMagicalElixir()
    {
        if (DateTime.UtcNow < nextElixirUseUtc)
        {
            return;
        }

        VnavmeshIpc.TryStop(vnav);
        var used = ActionManager.Instance()->UseAction(ActionType.EventItem, MagicalElixirEventItemId);
        nextElixirUseUtc = DateTime.UtcNow.AddSeconds(3);
        if (used)
        {
            awaitingHint = true;
            hintDeadlineUtc = DateTime.UtcNow.AddSeconds(5);
            RuntimeStatus = "マジカルエリクサーを使用しました。";
        }
        else
        {
            RuntimeStatus = "マジカルエリクサーを使用できる状態になるまで待っています。";
        }
    }

    private IGameObject? FindRevealedCoffer()
    {
        return Svc.Objects
            .Where(obj => obj is { ObjectKind: ObjectKind.EventObj, IsDead: false, IsTargetable: true }
                          && MagicPotCofferBaseIds.Contains(obj.BaseId)
                          && obj.IsValid())
            .Where(obj => target == null || HorizontalDistance(obj.Position, target.Value) <= CofferDetectionDistance)
            .OrderBy(obj => HorizontalDistance(Player.Position, obj.Position))
            .FirstOrDefault(obj => HorizontalDistance(Player.Position, obj.Position) <= 30f);
    }

    private unsafe void MoveToAndOpen(IGameObject coffer)
    {
        var distance = Player.DistanceTo(coffer);
        if (distance > 2f)
        {
            RuntimeStatus = $"出現した宝箱へ移動中です（残り {distance:F1}m）。";
            VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
            if (!isRunning)
            {
                VnavmeshIpc.TryPathfindAndMoveTo(vnav, coffer.Position, false);
            }

            return;
        }

        VnavmeshIpc.TryStop(vnav);
        RuntimeStatus = "宝箱を開封しています。";
        if (!EzThrottler.Throttle("MagicPotTreasure.Open", 1000))
        {
            return;
        }

        if (Player.Mounted)
        {
            Actions.Unmount.Cast();
            return;
        }

        Svc.Targets.Target = coffer;
        TargetSystem.Instance()->InteractWithObject((GameObject*)(void*)coffer.Address);
    }

    private Vector3? InferTarget()
    {
        var candidates = GetCandidates().ToArray();
        if (candidates.Length == 0 || hints.Count == 0)
        {
            return null;
        }

        return candidates
            .Select(candidate => (Candidate: candidate, Score: hints.Sum(hint => ScoreCandidate(candidate, hint))))
            .OrderBy(item => item.Score)
            .ThenBy(item => HorizontalDistance(Player.Position, item.Candidate))
            .First().Candidate;
    }

    private IEnumerable<Vector3> GetCandidates()
    {
        if (secondChance)
        {
            return MagicPotTreasureData.SecondChance;
        }

        return sourcePotFateId switch
        {
            2072 => MagicPotTreasureData.NorthPot,
            2073 => MagicPotTreasureData.SouthPot,
            _ => MagicPotTreasureData.NorthPot.Concat(MagicPotTreasureData.SouthPot),
        };
    }

    private static double ScoreCandidate(Vector3 candidate, HintSample hint)
    {
        var dx = candidate.X - hint.Position.X;
        var dz = candidate.Z - hint.Position.Z;
        var distance = Math.Sqrt(dx * dx + dz * dz);
        var bearing = NormalizeDegrees(Math.Atan2(dx, -dz) * 180d / Math.PI);
        var angleDifference = Math.Abs(NormalizeDegrees(bearing - hint.DirectionDegrees));
        if (angleDifference > 180d)
        {
            angleDifference = 360d - angleDifference;
        }

        var angularScore = Math.Pow(angleDifference / 22.5d, 2) * 8d;
        var distanceScore = hint.Range switch
        {
            HintRange.Immediate => DistanceBandPenalty(distance, 0d, 100d, 50d),
            HintRange.Near => DistanceBandPenalty(distance, 80d, 500d, 260d),
            HintRange.Far => DistanceBandPenalty(distance, 400d, 1000d, 700d),
            HintRange.VeryFar => DistanceBandPenalty(distance, 850d, 3000d, 1300d),
            _ => 0d,
        };

        return angularScore + distanceScore;
    }

    private static double DistanceBandPenalty(double distance, double minimum, double maximum, double ideal)
    {
        if (distance < minimum)
        {
            return 20d + Math.Pow((minimum - distance) / 100d, 2);
        }

        if (distance > maximum)
        {
            return 20d + Math.Pow((distance - maximum) / 100d, 2);
        }

        return Math.Abs(distance - ideal) / Math.Max(ideal, 1d);
    }

    private uint InferSourcePotFromPosition()
    {
        var position = Player.Position;
        var northDistance = HorizontalDistance(position, new Vector3(233f, 7.729229f, -470f));
        var southDistance = HorizontalDistance(position, new Vector3(-505.2822f, 53.14409f, 244.041f));
        return northDistance <= southDistance ? 2072u : 2073u;
    }

    private static bool TryParseHint(string text, out double direction, out HintRange range)
    {
        direction = 0d;
        range = HintRange.Unknown;
        var normalized = text.ToLowerInvariant().Replace("-", string.Empty).Replace(" ", string.Empty);

        if (normalized.Contains("とても近く") || normalized.Contains("immediately"))
            range = HintRange.Immediate;
        else if (normalized.Contains("とても遠く") || normalized.Contains("far,far") || normalized.Contains("farfar"))
            range = HintRange.VeryFar;
        else if (normalized.Contains("遠く") || normalized.Contains("farto"))
            range = HintRange.Far;
        else if (normalized.Contains("近く") || normalized.Contains("yousensesomethingto"))
            range = HintRange.Near;

        if (range == HintRange.Unknown)
        {
            return false;
        }

        var directions = new (string[] Tokens, double Degrees)[]
        {
            (["北東", "northeast"], 45d),
            (["南東", "southeast"], 135d),
            (["南西", "southwest"], 225d),
            (["北西", "northwest"], 315d),
            (["北", "north"], 0d),
            (["東", "east"], 90d),
            (["南", "south"], 180d),
            (["西", "west"], 270d),
        };

        foreach (var (tokens, degrees) in directions)
        {
            if (tokens.Any(normalized.Contains))
            {
                direction = degrees;
                return true;
            }
        }

        return false;
    }

    private static bool IsSecondChanceMessage(string text)
    {
        return text.Contains("2つめの財宝", StringComparison.OrdinalIgnoreCase)
               || text.Contains("another treasure coffer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCofferDiscoveredMessage(string text)
    {
        return text.Contains("財宝を発見", StringComparison.OrdinalIgnoreCase)
               || text.Contains("discover a treasure coffer", StringComparison.OrdinalIgnoreCase);
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to)
    {
        var dx = from.X - to.X;
        var dz = from.Z - to.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static double NormalizeDegrees(double degrees)
    {
        degrees %= 360d;
        return degrees < 0d ? degrees + 360d : degrees;
    }

    private readonly record struct HintSample(Vector3 Position, double DirectionDegrees, HintRange Range);

    private enum HintRange
    {
        Unknown,
        Immediate,
        Near,
        Far,
        VeryFar,
    }
}
