using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class TooltipAttribute(string translationKey) : Attribute
{
    public string translation_key { get; } = translationKey;
}
