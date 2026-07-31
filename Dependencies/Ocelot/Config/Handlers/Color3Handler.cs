using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace Ocelot.Config.Handlers;

public class Color3Handler(ModuleConfig self, ConfigAttribute attribute, PropertyInfo prop) : Handler(self, attribute, prop)
{
    protected override Type type
    {
        get => typeof(Vector3);
    }

    protected override (bool handled, bool changed) RenderComponent(RenderContext payload)
    {
        var value = (Vector3)(payload.GetValue() ?? Vector3.Zero);

        if (ImGui.ColorEdit3(payload.GetLabelWithId(), ref value))
        {
            payload.SetValue(value);
            return (true, true);
        }

        return (true, false);
    }
}
