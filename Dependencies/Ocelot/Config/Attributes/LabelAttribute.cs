using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class LabelAttribute(string translationKey) : Attribute
{
    public string translation_key { get; } = translationKey;
}
