using System;
using Dalamud.Bindings.ImGui;
using Ocelot.Windows;

namespace Ocelot.Gameplay;

public class ItemTracker
{
    public readonly Item Item;

    private DateTime start = DateTime.UtcNow;

    private int last = 0;

    private int gained = 0;

    public ItemTracker(Item item)
    {
        Item = item;
        last = Item.Count();
    }

    public void Reset()
    {
        start = DateTime.UtcNow;
        last = Item.Count();
        gained = 0;
    }

    public void Update()
    {
        var current = Item.Count();
        var delta = current - last;
        last = current;

        if (delta > 0)
        {
            if (gained == 0)
            {
                start = DateTime.UtcNow;
            }

            gained += delta;
        }
    }

    public float GetGainPerHour()
    {
        var elapsed = (float)(DateTime.UtcNow - start).TotalHours;
        if (elapsed <= 0)
        {
            return 0;
        }

        return MathF.Round(gained / elapsed, 2);
    }

    public void Render(RenderContext context)
    {
        Item.Render(context);

        var delta = GetGainPerHour();
        if (delta <= 0f)
        {
            return;
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"({delta:f2} p/h)");
    }
}
