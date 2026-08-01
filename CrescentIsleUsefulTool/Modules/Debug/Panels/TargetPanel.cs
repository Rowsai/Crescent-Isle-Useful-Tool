using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace CrescentIsleUsefulTool.Modules.Debug.Panels;

public class TargetPanel : Panel
{
    public override string GetName() => "Target";

    public override void Render(DebugModule module)
    {
        var target = Svc.Targets.Target;
        if (target == null || !target.IsValid())
        {
            ImGui.TextUnformatted("No valid target selected.");
            return;
        }

        OcelotUi.LabelledValue("Name", target.Name.ToString());
        OcelotUi.LabelledValue("Entity ID", target.EntityId);
        OcelotUi.LabelledValue("Base ID", target.BaseId);
        OcelotUi.LabelledValue("Object kind", target.ObjectKind);
        OcelotUi.LabelledValue("Sub kind", target.SubKind);
        OcelotUi.LabelledValue("Position", target.Position);
        OcelotUi.LabelledValue("Hitbox radius", target.HitboxRadius);
        OcelotUi.LabelledValue("Dead", target.IsDead);
        OcelotUi.LabelledValue("Targetable", target.IsTargetable);
    }
}
