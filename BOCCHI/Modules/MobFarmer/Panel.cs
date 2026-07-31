using System.Linq;
using BOCCHI.Ui;
using Dalamud.Bindings.ImGui;
using Ocelot;

namespace BOCCHI.Modules.MobFarmer;

public class Panel
{
    public void Draw(MobFarmerModule module)
    {
        CrescentTheme.Card("MobFarmer", "MOB FARMER", () =>
        {
            if (ImGui.Button(module.Farmer.Running ? I18N.T("generic.label.stop") : I18N.T("generic.label.start")))
            {
                module.Farmer.Toggle(module);
            }

            if (module.Farmer.Running)
            {
                ImGui.SameLine();
                ImGui.TextColored(CrescentTheme.Success, module.Farmer.StateMachine.State.ToString());
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
}
