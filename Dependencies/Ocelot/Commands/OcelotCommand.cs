using System;
using System.Collections.Generic;
using System.Linq;
using ECommons;

namespace Ocelot.Commands;

public abstract class OcelotCommand
{
    protected abstract string Command { get; }

    protected abstract string Description { get; }

    protected virtual IReadOnlyList<string> Aliases { get; set; } = [];

    protected virtual IReadOnlyList<string> ValidArguments { get; set; } = [];

    public void Register()
    {
        EzCmd.Add(Command, Handle, Description);
        foreach (var alias in Aliases)
        {
            EzCmd.Add(alias, Handle, $"Alias: {Command}", int.MaxValue);
        }
    }

    private bool ValidateArguments(string arguments)
    {
        if (string.IsNullOrEmpty(arguments))
        {
            return true;
        }

        return ValidArguments.Count == 0 || ValidArguments.Any(arg => string.Equals(arg, arguments, StringComparison.OrdinalIgnoreCase));
    }

    public void Handle(string command, string arguments)
    {
        if (!ValidateArguments(arguments))
        {
            Logger.Error($"Invalid argument ({arguments}) to command ({command})");
        }

        Execute(command, arguments);
    }

    public abstract void Execute(string command, string arguments);
}
