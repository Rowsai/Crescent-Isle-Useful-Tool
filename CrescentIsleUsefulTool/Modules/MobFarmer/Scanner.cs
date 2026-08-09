using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrescentIsleUsefulTool.Data;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameHelpers;

namespace CrescentIsleUsefulTool.Modules.MobFarmer;

public readonly record struct MobSnapshot(
    ulong EntityId,
    uint NameId,
    Vector3 Position,
    bool IsTargetingPlayer,
    bool HasTarget,
    byte Level);

public class Scanner(MobFarmerModule module)
{
    public IReadOnlyList<MobSnapshot> Mobs { get; private set; } = [];

    public IEnumerable<MobSnapshot> InCombat => Mobs.Where(mob => mob.IsTargetingPlayer);

    public IEnumerable<MobSnapshot> NotInCombat => Mobs.Where(mob => !mob.HasTarget);

    public void Tick(IFramework _)
    {
        Mobs = TargetHelper.Enemies
            .Where(obj => obj.IsValid() && Player.DistanceTo(obj) <= module.Config.MaxEuclideanDistance)
            .Where(IsSelectedMob)
            .Select(obj => (Enemy: obj, KnowledgeLevel: KnowledgeLevel.TryGet(obj)))
            .Where(obj => obj.KnowledgeLevel is null || obj.KnowledgeLevel.Value <= module.Config.MaxMobLevel)
            .Select(obj => new MobSnapshot(
                obj.Enemy.EntityId,
                obj.Enemy.NameId,
                obj.Enemy.Position,
                obj.Enemy.IsTargetingPlayer(),
                obj.Enemy.HasTarget(),
                obj.KnowledgeLevel ?? 0))
            .ToList();
    }

    public IGameObject? Resolve(MobSnapshot mob) => GameObjectInteraction.Resolve(mob.EntityId);

    public IGameObject? ResolveCentroid(IEnumerable<MobSnapshot> mobs)
    {
        var snapshots = mobs.ToArray();
        if (snapshots.Length == 0)
        {
            return null;
        }

        var center = snapshots.Aggregate(Vector3.Zero, (sum, mob) => sum + mob.Position) / snapshots.Length;
        return Resolve(snapshots.OrderBy(mob => Vector3.DistanceSquared(mob.Position, center)).First());
    }

    private bool IsSelectedMob(IBattleNpc obj)
    {
        if (module.Config.Mobs.Contains((Mob)obj.NameId))
        {
            return true;
        }

        return module.Config.ConsiderSpecialMobs && MobData.MobsWithSpawnCondition.Contains((Mob)obj.NameId);
    }
}
