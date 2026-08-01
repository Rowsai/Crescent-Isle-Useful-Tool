using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace CrescentIsleUsefulTool;

/// <summary>
/// Resolves a game object again from the current object table immediately
/// before interaction. Dalamud object wrappers must never be retained and
/// dereferenced after the frame in which they were observed.
/// </summary>
internal static class GameObjectInteraction
{
    public static IGameObject? Resolve(ulong entityId)
    {
        return Svc.Objects.FirstOrDefault(obj => obj.EntityId == entityId && obj.IsValid());
    }

    public static unsafe bool TryInteract(ulong entityId, float maximumDistance, Vector3? expectedPosition = null)
    {
        var current = Resolve(entityId);
        if (current == null || current.Address == nint.Zero || !current.IsTargetable || current.IsDead)
        {
            return false;
        }

        if (Player.DistanceTo(current) > maximumDistance)
        {
            return false;
        }

        if (expectedPosition is { } position && Vector3.Distance(current.Position, position) > 5f)
        {
            return false;
        }

        var targetSystem = TargetSystem.Instance();
        if (targetSystem == null)
        {
            return false;
        }

        var address = current.Address;
        Svc.Targets.Target = current;
        targetSystem->InteractWithObject((GameObject*)(void*)address);
        return true;
    }
}
