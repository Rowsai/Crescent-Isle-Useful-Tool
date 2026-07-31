using System;

namespace Ocelot.Modules;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class InjectIpcAttribute(bool required = false) : Attribute
{
    public bool Required = required;
}
