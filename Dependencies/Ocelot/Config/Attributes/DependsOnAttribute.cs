using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class DependsOnAttribute : Attribute
{
    public string[] dependencies { get; }

    public DependsOnAttribute(params string[] dependencies)
    {
        this.dependencies = dependencies;
    }
}
