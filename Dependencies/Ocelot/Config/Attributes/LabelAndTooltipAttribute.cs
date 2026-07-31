using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class LabelAndTooltipAttribute(string? key = null) : Attribute
{
    public string? translation_key { get; } = key;
}
