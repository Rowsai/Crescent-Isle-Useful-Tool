using System;
using System.Reflection;
using Ocelot.Config.Handlers;
using Ocelot.Modules;

namespace Ocelot.Config.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class CheckboxAttribute : ConfigAttribute
{
    public override Handler GetHandler(ModuleConfig self, ConfigAttribute attr, PropertyInfo prop)
    {
        return new CheckboxHandler(self, attr, prop);
    }
}
