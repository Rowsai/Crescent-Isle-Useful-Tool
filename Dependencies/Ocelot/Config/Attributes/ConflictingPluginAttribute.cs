using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public class ConflictingPluginAttribute : Attribute
{
    public string[] conflicts { get; }

    public ConflictingPluginAttribute(params string[] conflicts)
    {
        this.conflicts = conflicts;
    }
}
