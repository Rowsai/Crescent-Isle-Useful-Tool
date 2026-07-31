using System;
using System.Linq;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Modules.Teleporter;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace CrescentIsleUsefulTool.Modules.Fates;

public class Panel
{
    public void Draw(FatesModule module)
    {
        CrescentTheme.Card("ActiveFates", module.T("panel.title"), () =>
        {
            if (module.tracker.Fates.Count <= 0)
            {
                CrescentTheme.EmptyState(module.T("panel.none"));
                return;
            }

            foreach (var fate in module.fates.Values)
            {
                if (!ZoneData.IsInOccultCrescent())
                {
                    module.fates.Clear();
                    return;
                }

                try
                {
                ImGui.TextUnformatted(fate.Name);
                ImGui.SameLine();
                ImGui.TextColored(CrescentTheme.AccentSoft, $"{fate.CurrentProgress}%");
                }
                catch (AccessViolationException)
                {
                    continue;
                }


                var estimate = fate.Progress.EstimateTimeToCompletion();
                if (estimate != null)
                {
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"({module.T("panel.estimated")} {estimate.Value:mm\\:ss})");
                }

                if (ZoneData.IsInNorthHorn() && fate.IsPotFate())
                {
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"({module.T("panel.spawned_at")} {fate.SpawnedAt:HH:mm:ss})");
                }


                if (module.TryGetModule<TeleporterModule>(out var teleporter) && teleporter!.IsReady())
                {
                    teleporter.teleporter.Button(fate.Data.Aethernet, fate.StartPosition, fate.Name, $"fate_{fate.Id}", fate.Data);
                }

                OcelotUi.Indent(() => EventIconRenderer.Drops(fate.Data, module.PluginConfig.EventDropConfig));

                if (!fate.Equals(module.fates.Values.Last()))
                {
                    OcelotUi.VSpace();
                }
            }
        }, "発生中のFATEと進行状況", CrescentTheme.Accent);
    }
}
