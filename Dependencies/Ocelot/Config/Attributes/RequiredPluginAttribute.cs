using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public class RequiredPluginAttribute : Attribute
{
    public string[] dependencies { get; }

    public RequiredPluginAttribute(params string[] dependencies)
    {
        this.dependencies = dependencies;
    }
}
