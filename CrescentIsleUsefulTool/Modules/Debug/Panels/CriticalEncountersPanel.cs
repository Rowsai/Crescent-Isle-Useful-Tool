using System;
using CrescentIsleUsefulTool.Modules.CriticalEncounters;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Ui;

namespace CrescentIsleUsefulTool.Modules.Debug.Panels;

/// <summary>
/// Debug output intentionally uses the managed CE snapshot. Reading the raw
/// DynamicEvent here after a despawn used to make the debug window capable of
/// crashing the game process.
/// </summary>
public class CriticalEncountersPanel : Panel
{
    public override string GetName() => "Critical Encounters";

    public override void Render(DebugModule module)
    {
        OcelotUi.Title("Critical Encounters:");
        var encounters = module.GetModule<CriticalEncountersModule>().CriticalEncounters;
        foreach (var ev in encounters.Values)
        {
            ImGui.TextUnformatted(ev.Name);
            ImGui.SameLine();
            ImGui.TextUnformatted(GetStateText(ev));

            if (ImGui.CollapsingHeader($"Snapshot##{ev.DynamicEventId}"))
            {
                OcelotUi.LabelledValue("ID", ev.DynamicEventId);
                OcelotUi.LabelledValue("State", ev.State);
                OcelotUi.LabelledValue("Progress", $"{ev.Progress}%");
                OcelotUi.LabelledValue("Event type", ev.EventType);
                OcelotUi.LabelledValue("Position", ev.Position);
            }
        }
    }

    private static string GetStateText(CriticalEncounterSnapshot ev)
    {
        if (ev.State == DynamicEventState.Register)
        {
            var start = DateTimeOffset.FromUnixTimeSeconds(ev.StartTimestamp).UtcDateTime;
            var remaining = start - DateTime.UtcNow;
            return $"(Preparing: {remaining:mm\\:ss})";
        }

        return ev.State == DynamicEventState.Battle ? $"({ev.Progress}%)" : $"({ev.State})";
    }
}
