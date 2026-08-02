using System.Linq;
using CrescentIsleUsefulTool.Modules.MobFarmer.States;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Bindings.ImGui;
using Ocelot;

namespace CrescentIsleUsefulTool.Modules.MobFarmer;

public class Panel
{
    public void Draw(MobFarmerModule module)
    {
        CrescentTheme.Card("MobFarmer", "モブ討伐支援", () =>
        {
            if (ImGui.Button(module.Farmer.Running ? I18N.T("generic.label.stop") : I18N.T("generic.label.start")))
            {
                module.Farmer.Toggle(module);
            }

            if (module.Farmer.Running)
            {
                ImGui.SameLine();
                ImGui.TextColored(CrescentTheme.Success, GetPhaseLabel(module.Farmer.StateMachine.State));
            }

            ImGui.Spacing();
            ImGui.TextDisabled("未交戦");
            ImGui.SameLine();
            ImGui.TextUnformatted(module.Scanner.NotInCombat.Count().ToString());
            ImGui.SameLine();
            ImGui.TextDisabled("  /  交戦中");
            ImGui.SameLine();
            ImGui.TextColored(CrescentTheme.Warning, module.Scanner.InCombat.Count().ToString());
        }, "周辺エネミーの自動戦闘状態");
    }

    private static string GetPhaseLabel(FarmerPhase phase)
    {
        return phase switch
        {
            FarmerPhase.Waiting => "待機中",
            FarmerPhase.Buffing => "戦闘準備中",
            FarmerPhase.Gathering => "敵を集めています",
            FarmerPhase.Stacking => "敵をまとめています",
            FarmerPhase.Fighting => "戦闘中",
            _ => "状態不明",
        };
    }
}
