using System;
using System.Reflection;
using Ocelot.Config.Handlers;
using Ocelot.Modules;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class IntRangeAttribute : ConfigAttribute
{
    public int min { get; }

    public int max { get; }

    public IntRangeAttribute(int min, int max)
    {
        this.min = min;
        this.max = max;
    }

    public override Handler GetHandler(ModuleConfig self, ConfigAttribute attr, PropertyInfo prop)
    {
        return new IntRangeHandler(self, attr, prop);
    }
}
