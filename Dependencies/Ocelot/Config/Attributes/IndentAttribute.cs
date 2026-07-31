using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class IndentAttribute : Attribute
{
    public uint depth { get; }

    public IndentAttribute(uint depth = 1)
    {
        this.depth = depth * 32;
    }
}
