using System;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace Ocelot.Config.Handlers;

public class CheckboxHandler(ModuleConfig self, ConfigAttribute attribute, PropertyInfo prop) : Handler(self, attribute, prop)
{
    protected override Type type
    {
        get => typeof(bool);
    }

    protected override (bool handled, bool changed) RenderComponent(RenderContext payload)
    {
        var value = (bool)(payload.GetValue() ?? false);

        if (ImGui.Checkbox(payload.GetLabelWithId(), ref value))
        {
            payload.SetValue(value);
            return (true, true);
        }

        return (true, false);
    }
}
