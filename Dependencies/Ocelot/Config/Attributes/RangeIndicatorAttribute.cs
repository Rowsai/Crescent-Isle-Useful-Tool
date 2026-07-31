using System;
using System.Numerics;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class RangeIndicatorAttribute() : Attribute
{
    public readonly Vector4? color;

    public RangeIndicatorAttribute(float r, float g, float b, float a = 1f) : this()
    {
        color = new Vector4(r, g, b, a);
    }
}
