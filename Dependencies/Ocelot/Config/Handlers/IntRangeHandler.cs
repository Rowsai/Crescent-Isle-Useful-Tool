using System;
using System.Reflection;
using ECommons.GameHelpers;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Attributes;
using Ocelot.Modules;
using Pictomancy;

namespace Ocelot.Config.Handlers;

public class IntRangeHandler : Handler
{
    protected override Type type
    {
        get => typeof(int);
    }

    public IntRangeHandler(ModuleConfig self, ConfigAttribute attribute, PropertyInfo prop)
        : base(self, attribute, prop)
    {
    }

    protected override (bool handled, bool changed) RenderComponent(RenderContext payload)
    {
        var value = (int)(payload.GetValue() ?? 1f);

        var attribute = this.attribute as IntRangeAttribute;
        if (ImGui.SliderInt(payload.GetLabelWithId(), ref value, attribute!.min, attribute!.max))
        {
            payload.SetValue(value);
            return (true, true);
        }

        var range = property.GetCustomAttribute<RangeIndicatorAttribute>();
        if (ImGui.IsItemHovered() && range != null)
        {
            PctService.VfxRenderer.AddCircle($"{property.Name}", Player.Position, value, range.color);
        }

        return (true, false);
    }
}
