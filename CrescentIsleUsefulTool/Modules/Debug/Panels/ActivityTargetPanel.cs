using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace CrescentIsleUsefulTool.Modules.Debug.Panels;

public class ActivityTargetPanel : Panel
{
    private readonly record struct ObjectSnapshot(
        string Name,
        ulong EntityId,
        ObjectKind Kind,
        bool IsTargetable,
        bool IsDead,
        Vector3 Position);

    private List<ObjectSnapshot> enemies = [];

    public override string GetName() => "Activity Targets";

    public override void Render(DebugModule module)
    {
        if (EzThrottler.Throttle("ActivityTargetPanel", 1000))
        {
            enemies = Svc.Objects
                .Where(obj => obj.IsValid() && Player.DistanceTo(obj) <= 50f && obj.ObjectKind == ObjectKind.BattleNpc)
                .Select(obj => new ObjectSnapshot(
                    obj.Name.ToString(), obj.EntityId, obj.ObjectKind, obj.IsTargetable, obj.IsDead, obj.Position))
                .ToList();
        }

        foreach (var enemy in enemies)
        {
            ImGui.TextUnformatted(enemy.Name);
            OcelotUi.Indent(() =>
            {
                OcelotUi.LabelledValue("Entity ID", enemy.EntityId);
                OcelotUi.LabelledValue("Object kind", enemy.Kind);
                OcelotUi.LabelledValue("Targetable", enemy.IsTargetable);
                OcelotUi.LabelledValue("Alive", !enemy.IsDead);
                OcelotUi.LabelledValue("Position", enemy.Position);
            });
        }
    }
}
