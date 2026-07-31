using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ui;
using Dalamud.Interface;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;

namespace CrescentIsleUsefulTool.Modules.Buff;

public class Panel
{
    public void Draw(BuffModule module)
    {
        CrescentTheme.Card("Buff", module.T("panel.title"), () =>
        {
            var isNearKnowledgeCrystal = ZoneData.IsNearKnowledgeCrystal();
            var isQueued = module.BuffManager.IsQueued();

            if (ImGuiEx.IconButton(FontAwesomeIcon.Redo, "Button##ApplyBuffs", enabled: isNearKnowledgeCrystal && !isQueued))
            {
                module.BuffManager.QueueBuffs();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(module.T("panel.button.tooltip"));
            }

            ImGui.SameLine();
            ImGui.TextDisabled(
                isQueued ? "適用処理中です。" : isNearKnowledgeCrystal ? "知識の結晶からバフを更新できます。" : "知識の結晶の近くで使用できます。"
            );
        }, "ナレッジバフをまとめて更新します。");
    }
}
