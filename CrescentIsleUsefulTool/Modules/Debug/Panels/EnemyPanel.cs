using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace CrescentIsleUsefulTool.Modules.Debug.Panels;

public class EnemyPanel : Panel
{
    private readonly record struct EnemySnapshot(
        string Name,
        ulong EntityId,
        uint BaseId,
        uint NameId,
        ObjectKind Kind,
        byte SubKind,
        Vector3 Position,
        float HitboxRadius,
        bool IsDead,
        bool IsTargetable);

    private List<EnemySnapshot> enemies = [];

    public override string GetName() => "Nearby Enemies";

    public override void Render(DebugModule module)
    {
        foreach (var enemy in enemies)
        {
            if (!ImGui.CollapsingHeader($"{enemy.Name} - {enemy.BaseId}##{enemy.EntityId}"))
            {
                continue;
            }

            OcelotUi.Indent(() =>
            {
                OcelotUi.LabelledValue("Entity ID", enemy.EntityId);
                OcelotUi.LabelledValue("Name ID", enemy.NameId);
                OcelotUi.LabelledValue("Object kind", enemy.Kind);
                OcelotUi.LabelledValue("Sub kind", enemy.SubKind);
                OcelotUi.LabelledValue("Position", enemy.Position);
                OcelotUi.LabelledValue("Hitbox radius", enemy.HitboxRadius);
                OcelotUi.LabelledValue("Dead", enemy.IsDead);
                OcelotUi.LabelledValue("Targetable", enemy.IsTargetable);
            });
        }
    }

    public override void Update(DebugModule module)
    {
        if (!EzThrottler.Throttle("enemies", 2000))
        {
            return;
        }

        enemies = Svc.Objects.OfType<IBattleNpc>()
            .Where(obj => obj.IsValid() && obj.IsHostile() && obj.IsTargetable)
            .OrderBy(obj => Vector3.Distance(obj.Position, Player.Position))
            .Select(obj => new EnemySnapshot(
                obj.Name.ToString(), obj.EntityId, obj.BaseId, obj.NameId, obj.ObjectKind,
                obj.SubKind, obj.Position, obj.HitboxRadius, obj.IsDead, obj.IsTargetable))
            .ToList();
    }
}
