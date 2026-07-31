using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Ocelot.Extensions;
using Ocelot.Ui;
using Ocelot.Windows;
using ItemData = Lumina.Excel.Sheets.Item;

namespace Ocelot.Gameplay;

public unsafe class Item(uint id)
{
    public readonly ItemData Data = Svc.Data.GetExcelSheet<ItemData>().GetRow(id);

    public int Count()
    {
        try
        {
            return InventoryManager.Instance()->GetInventoryItemCount(id);
        }
        catch
        {
            return 0;
        }
    }

    public virtual void Use()
    {
        try
        {
            AgentInventoryContext.Instance()->UseItem(id);
        }
        catch
        {
            // ignored
        }
    }

    public void Render(RenderContext context)
    {
        var value = $"{Count()}";
        if (Data.StackSize > 0 && Data.StackSize % 1000 != 999)
        {
            value += $"/{Data.StackSize}";
        }

        OcelotUi.LabelledValue(Data.Plural.ExtractText().ToTitleCase(), value);
        if (Data.StackSize > 0 && Data.StackSize % 1000 != 999)
        {
            OcelotUi.ProgressBar(Count() / (float)Data.StackSize);
        }
    }
}
