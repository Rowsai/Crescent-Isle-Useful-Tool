using System;
using System.Numerics;
using Dalamud.Interface;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class IconAttribute : Attribute
{
    public readonly FontAwesomeIcon icon;

    public readonly string tooltip_translation_key;

    public readonly Vector4 color;

    public IconAttribute(FontAwesomeIcon icon, string tooltip_translation_key = "", float r = 1f, float g = 1f, float b = 1f, float a = 1f)
    {
        this.icon = icon;
        this.tooltip_translation_key = tooltip_translation_key;
        color = new Vector4(r, g, b, a);
    }
}
