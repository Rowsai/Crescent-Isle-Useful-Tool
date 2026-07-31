using System;
using System.Collections.Generic;
using System.Linq;
using Ocelot.Modules;

namespace Ocelot.Commands;

public class CommandManager : IDisposable
{
    private readonly List<OcelotCommand> commands = new();

    public void Initialize(OcelotPlugin plugin)
    {
        var commandTypes = Registry.GetTypesWithAttribute<OcelotCommandAttribute>()
            .Where(t => typeof(OcelotCommand).IsAssignableFrom(t))
            .ToList();

        foreach (var type in commandTypes)
        {
            Logger.Info($"Registering command: {type.FullName}");
            var instance = (OcelotCommand)Activator.CreateInstance(type, plugin)!;
            commands.Add(instance);
            instance.Register();
        }
    }

    public void Dispose()
    {
    }
}
