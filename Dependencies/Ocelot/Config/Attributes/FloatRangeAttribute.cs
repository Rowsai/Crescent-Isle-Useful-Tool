using System;
using System.Reflection;
using Ocelot.Config.Handlers;
using Ocelot.Modules;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class FloatRangeAttribute : ConfigAttribute
{
    public float min { get; }

    public float max { get; }

    public FloatRangeAttribute(float min, float max)
    {
        this.min = min;
        this.max = max;
    }

    public override Handler GetHandler(ModuleConfig self, ConfigAttribute attr, PropertyInfo prop)
    {
        return new FloatRangeHandler(self, attr, prop);
    }
}
