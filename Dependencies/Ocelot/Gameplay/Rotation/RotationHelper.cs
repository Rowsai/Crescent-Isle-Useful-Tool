using System;
using System.Collections.Generic;
using ECommons.Reflection;
using Ocelot.Modules;

namespace Ocelot.Gameplay.Rotation;

public static class RotationHelper
{
    private readonly static Dictionary<string, Func<IModule, IRotationPlugin>> RotationPlugins = new()
    {
        { "WrathCombo", m => new Wrath(m) },
    };

    public static IRotationPlugin GetPlugin(IModule module)
    {
        foreach (var (plugin, factory) in RotationPlugins)
        {
            if (!DalamudReflector.TryGetDalamudPlugin(plugin, out _, false, true))
            {
                continue;
            }

            return factory(module);
        }

        return new BlankRotationPlugin();
    }
}
