using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using Ocelot;
using Ocelot.Data;
using Ocelot.Modules;

public class PluginWatcher
{
    private Dictionary<string, bool> previousPluginList;

    public event Action<string>? OnPluginEnabled;

    public event Action<string>? OnPluginDisabled;

    public event Action? OnPluginListChanged;

    public IReadOnlyDictionary<string, bool> CurrentPlugins
    {
        get => previousPluginList;
    }

    public PluginWatcher()
    {
        previousPluginList = BuildPluginList();
    }

    public void Update(UpdateContext context)
    {
        if (!Gate.Milliseconds(this, 1000, context))
        {
            return;
        }

        var currentPluginList = BuildPluginList();

        foreach (var kvp in currentPluginList)
        {
            if (previousPluginList.TryGetValue(kvp.Key, out var prevVal))
            {
                if (prevVal != kvp.Value)
                {
                    if (kvp.Value)
                    {
                        OnPluginEnabled?.Invoke(kvp.Key);
                    }
                    else
                    {
                        OnPluginDisabled?.Invoke(kvp.Key);
                    }
                }
            }
            else
            {
                if (kvp.Value)
                {
                    OnPluginEnabled?.Invoke(kvp.Key);
                }
            }
        }

        foreach (var kvp in previousPluginList)
        {
            if (!currentPluginList.ContainsKey(kvp.Key))
            {
                OnPluginDisabled?.Invoke(kvp.Key);
            }
        }

        if (HasChanges(currentPluginList, previousPluginList))
        {
            OnPluginListChanged?.Invoke();
        }

        previousPluginList = currentPluginList;
    }

    private Dictionary<string, bool> BuildPluginList()
    {
        var result = new Dictionary<string, bool>();
        foreach (var plugin in Svc.PluginInterface.InstalledPlugins)
        {
            var key = GetPluginKey(plugin);
            result[key] = plugin.IsLoaded;
        }

        return result;
    }

    private static string GetPluginKey(IExposedPlugin plugin)
    {
        var key = $"{plugin.InternalName}.{plugin.Version}";
        if (plugin.IsDev)
        {
            key += ".Dev";
        }

        return key;
    }

    private bool HasChanges(Dictionary<string, bool> current, Dictionary<string, bool> previous)
    {
        if (current.Count != previous.Count)
        {
            return true;
        }

        foreach (var kvp in current)
        {
            if (!previous.TryGetValue(kvp.Key, out var oldVal) || oldVal != kvp.Value)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPluginEnabled(string name, bool includeDev = true)
    {
        if (previousPluginList.TryGetValue(name, out var enabled) && enabled)
        {
            return true;
        }

        return includeDev && previousPluginList.TryGetValue($"{name}.Dev", out var devEnabled) && devEnabled;
    }

    public bool IsOcelotPluginEnabled(OcelotPlugins plugin)
    {
        return IsPluginEnabled(plugin.GetInternalName());
    }
}
