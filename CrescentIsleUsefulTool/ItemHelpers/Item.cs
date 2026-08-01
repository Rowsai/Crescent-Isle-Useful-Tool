using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace CrescentIsleUsefulTool.ItemHelpers;

public unsafe class Item(uint id)
{
    public int Count()
    {
        var manager = InventoryManager.Instance();
        return manager == null ? 0 : manager->GetInventoryItemCount(id);
    }

    public void Use()
    {
        var agent = AgentInventoryContext.Instance();
        if (agent != null)
        {
            agent->UseItem(id);
        }
    }
}
