using System;
using System.Collections.Generic;
using System.Linq;
using BOCCHI.Data;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;

namespace BOCCHI.Modules.Automator;

public class Panel
{
    private static readonly string[] NorthHornEncounters =
    [
        "変化の使い魔『メタモルフォア』",
        "求道の人造人間『エルムギガース』",
        "絶島の誘拐者『アブダクター』",
        "大食の呪鬼『アルゴル』",
        "魔導兵団『タイニーメイジ』",
        "覚醒の多頭竜『マギ・ヒドラ』",
        "反逆の使い魔『アトラス・カーバンクル』",
        "禁忌の魔道書『アルバデル』",
        "死霊使いの亡霊『マギ・ネクロマンサー』",
        "呪いを継ぐ者『ベイルマギア』",
        "白の守護者『アラバスターブレード』",
        "魔女の複製体『カロフィステリ・ダブル』",
        "暗紅の魔竜『ルブルムドラゴン』",
        "？？？（ヘルハウンド）",
    ];

    public void Draw(AutomatorModule module)
    {
        OcelotUi.Title($"{module.T("panel.title")}:");
        OcelotUi.Indent(() =>
        {
            OcelotUi.Title($"{module.T("panel.activity.label")}:");
            try
            {
                var name = module.automator.Activity?.GetName() ?? module.T("panel.activity.none");
                ImGui.SameLine();
                ImGui.TextUnformatted(name);
            }
            catch (AccessViolationException)
            {
                return;
            }

            OcelotUi.Title($"{module.T("panel.activity_state.label")}:");
            ImGui.SameLine();
            ImGui.TextUnformatted(module.automator.Activity?.state.ToLabel() ?? module.T("panel.activity_state.none"));
        });

        DrawSupportedCriticalEncounters();
    }

    private static void DrawSupportedCriticalEncounters()
    {
        OcelotUi.Title("不正モード対応 CE:");
        if (!ImGui.BeginTabBar("##AutomatorEncounterList"))
        {
            return;
        }

        if (ImGui.BeginTabItem("南征編 (South Horn)"))
        {
            var southHorn = EventData.CriticalEncounters.Values
                .Where(data => data.Id is >= 33 and <= 47)
                .OrderBy(data => data.Id)
                .Select(data => data.InternalName);
            DrawEncounterTable(southHorn);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("北征編 (North Horn)"))
        {
            DrawEncounterTable(NorthHornEncounters);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private static void DrawEncounterTable(IEnumerable<string> encounters)
    {
        if (!ImGui.BeginTable("##SupportedEncounters", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg))
        {
            return;
        }

        foreach (var encounter in encounters)
        {
            ImGui.TableNextColumn();
            ImGui.TextWrapped(encounter);
        }

        ImGui.EndTable();
    }
}
