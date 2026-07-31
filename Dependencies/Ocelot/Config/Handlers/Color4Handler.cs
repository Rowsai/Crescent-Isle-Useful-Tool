using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace Ocelot.Config.Handlers;

public class Color4Handler(ModuleConfig self, ConfigAttribute attribute, PropertyInfo prop) : Handler(self, attribute, prop)
{
    protected override Type type
    {
        get => typeof(Vector4);
    }

    protected override (bool handled, bool changed) RenderComponent(RenderContext payload)
    {
        var value = (Vector4)(payload.GetValue() ?? Vector4.Zero);

        if (ImGui.ColorEdit4(payload.GetLabelWithId(), ref value))
        {
            payload.SetValue(value);
            return (true, true);
        }

        return (true, false);
    }
}
