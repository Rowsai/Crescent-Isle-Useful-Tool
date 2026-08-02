using System;
using System.Linq;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Modules.Teleporter;
using CrescentIsleUsefulTool.Ui;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace CrescentIsleUsefulTool.Modules.CriticalEncounters;

public class Panel
{
    public void Draw(CriticalEncountersModule module)
    {
        CrescentTheme.Card("CriticalEncounters", module.T("panel.title"), () =>
        {
            var active = module.CriticalEncounters.Values.Count(ev => ev.State != DynamicEventState.Inactive);
            if (active <= 0)
            {
                CrescentTheme.EmptyState(module.T("panel.none"));
                return;
            }

            foreach (var ev in module.CriticalEncounters.Values)
            {
                if (!ZoneData.IsInOccultCrescent())
                {
                    module.CriticalEncounters.Clear();
                    return;
                }

                if (ev.EventType >= 4 && ZoneData.IsInSouthHorn())
                {
                    HandleTower(ev, module);
                    continue;
                }

                if (ev.State == DynamicEventState.Inactive)
                {
                    continue;
                }

                var data = EventData.GetCriticalEncounter(ev.DynamicEventId);

                var displayName = EventData.GetCriticalEncounterDisplayName(ev.DynamicEventId);
                ImGui.TextUnformatted(displayName);

                switch (ev.State)
                {
                    case DynamicEventState.Register:
                        {
                            var start = DateTimeOffset.FromUnixTimeSeconds(ev.StartTimestamp).DateTime;
                            var timeUntilStart = start - DateTime.UtcNow;
                            var formattedTime = $"{timeUntilStart.Minutes:D2}:{timeUntilStart.Seconds:D2}";

                            ImGui.SameLine();
                            ImGui.TextUnformatted($"({module.T("panel.register")}: {formattedTime})");
                            break;
                        }
                    case DynamicEventState.Warmup:
                        ImGui.SameLine();
                        ImGui.TextUnformatted($"({module.T("panel.warmup")})");
                        break;
                    case DynamicEventState.Battle:
                        {
                            ImGui.SameLine();
                            ImGui.TextUnformatted($"({ev.Progress}%)");

                            if (module.Progress.TryGetValue(ev.DynamicEventId, out var progress))
                            {
                                var estimate = progress.EstimateTimeToCompletion();
                                if (estimate != null)
                                {
                                    ImGui.SameLine();
                                    ImGui.TextUnformatted($"({module.T("panel.estimated")} {estimate.Value:mm\\:ss})");
                                }
                            }

                            break;
                        }
                    case DynamicEventState.Inactive:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (ev.State != DynamicEventState.Register)
                {
                    OcelotUi.Indent(() => EventIconRenderer.Drops(data, module.PluginConfig.EventDropConfig));
                    continue;
                }

                if (module.TryGetModule<TeleporterModule>(out var teleporter) && teleporter!.IsReady())
                {
                    var start = ev.Position;

                    teleporter.teleporter.Button(data.Aethernet, start, displayName, $"ce_{ev.DynamicEventId}", data);
                }

                OcelotUi.Indent(() => EventIconRenderer.Drops(data, module.PluginConfig.EventDropConfig));
            }
        }, "参加可能なCEと開始・進行状況", CrescentTheme.Warning);
    }


    private void HandleTower(CriticalEncounterSnapshot ev, CriticalEncountersModule module)
    {
        if (!module.Config.TrackForkedTower || ev.State == DynamicEventState.Battle)
        {
            return;
        }

        OcelotUi.Error("この機能は現在調整中です。");

        if (ev.State == DynamicEventState.Inactive)
        {
            ImGui.TextUnformatted($"{ev.Name}：");

            var time = module.Tracker.TowerTimer.GetTimeToForkedTowerSpawn(ev.State);
            OcelotUi.Indent(() => { OcelotUi.LabelledValue("フォークタワー出現予想", $"{time:mm\\:ss}"); });
        }
        else
        {
            ImGui.TextUnformatted($"{ev.Name}：");

            var time = module.Tracker.TowerTimer.GetTimeRemainingToRegister(ev.State);
            OcelotUi.Indent(() => { OcelotUi.LabelledValue("フォークタワー受付終了まで", $"{time:mm\\:ss}"); });
        }

        OcelotUi.Indent(32, () =>
        {
            OcelotUi.LabelledValue("完了したCE", module.Tracker.TowerTimer.CriticalEncountersCompleted);
            OcelotUi.LabelledValue("完了したFATE", module.Tracker.TowerTimer.FatesCompleted);
        });


        if (!TowerHelper.IsPlayerNearTower(TowerHelper.TowerType.Blood))
        {
            return;
        }

        OcelotUi.Indent(() =>
        {
            OcelotUi.LabelledValue("足場上のプレイヤー", TowerHelper.GetPlayersInTowerZone(TowerHelper.TowerType.Blood));
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("自分のキャラクターを含みます");
            }

            OcelotUi.LabelledValue("足場付近のプレイヤー", TowerHelper.GetPlayersNearTowerZone(TowerHelper.TowerType.Blood));
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("自分のキャラクターを含みます");
            }
        });
    }
}
