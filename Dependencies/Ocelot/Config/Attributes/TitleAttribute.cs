using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class TitleAttribute(string TranslationKey = "config.title") : Attribute
{
    public string TranslationKey { get; } = TranslationKey;
}
