using System;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class TextAttribute : Attribute
{
    public string translation_key { get; }

    public TextAttribute(string translation_key)
    {
        this.translation_key = translation_key;
    }
}
