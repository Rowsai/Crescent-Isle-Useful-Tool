using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace CrescentIsleUsefulTool.Modules.CriticalEncounters;

/// <summary>
/// Managed copy of the scalar CE data needed outside the current framework
/// update. DynamicEvent contains pointers owned by the game and is unsafe to
/// retain after the event table changes.
/// </summary>
public sealed record CriticalEncounterSnapshot(
    uint DynamicEventId,
    DynamicEventState State,
    byte Progress,
    uint EventType,
    uint StartTimestamp,
    Vector3 Position,
    string Name);
