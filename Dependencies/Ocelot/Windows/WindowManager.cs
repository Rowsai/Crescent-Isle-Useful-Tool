using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.DalamudServices;

namespace Ocelot.Windows;

public class WindowManager : IDisposable
{
    private readonly WindowSystem manager = new($"Ocelot##{Svc.PluginInterface?.InternalName}");

    private OcelotMainWindow?
        mainWindow;

    private OcelotConfigWindow? configWindow;

    private List<OcelotWindow> windows = [];

    public void Initialize(OcelotPlugin plugin, IOcelotConfig config)
    {
        if (Registry.GetTypesWithAttribute<OcelotMainWindowAttribute>().TryGetFirst(out var mainWindowType))
        {
            mainWindow = Activator.CreateInstance(mainWindowType, plugin, config) as OcelotMainWindow;
            if (mainWindow != null)
            {
                manager.AddWindow(mainWindow);
                windows.Add(mainWindow);

                mainWindow.PreInitialize();
                mainWindow.Initialize();
            }
        }

        if (Registry.GetTypesWithAttribute<OcelotConfigWindowAttribute>().TryGetFirst(out var configWindowType))
        {
            configWindow = Activator.CreateInstance(configWindowType, plugin, config) as OcelotConfigWindow;
            if (configWindow != null)
            {
                manager.AddWindow(configWindow);
                windows.Add(configWindow);

                configWindow.PreInitialize();
                configWindow.Initialize();
            }
        }

        foreach (var type in Registry.GetTypesWithAttribute<OcelotWindowAttribute>())
        {
            var instance = Activator.CreateInstance(type, plugin, config) as OcelotWindow;
            if (instance != null)
            {
                manager.AddWindow(instance);
                windows.Add(instance);

                instance.PreInitialize();
                instance.Initialize();
            }
        }

        foreach (var window in windows)
        {
            window.PostInitialize();
        }


        Svc.PluginInterface.UiBuilder.Draw += manager.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void ToggleConfigUI()
    {
        configWindow?.Toggle();
    }

    public bool IsConfigUIOpen()
    {
        return configWindow?.IsOpen ?? false;
    }

    public void OpenConfigUI()
    {
        if (!IsConfigUIOpen())
        {
            ToggleConfigUI();
        }
    }

    public void CloseConfigUI()
    {
        if (IsConfigUIOpen())
        {
            ToggleConfigUI();
        }
    }

    public void ToggleMainUI()
    {
        mainWindow?.Toggle();
    }

    public bool IsMainUIOpen()
    {
        return mainWindow?.IsOpen ?? false;
    }

    public void OpenMainUI()
    {
        if (!IsMainUIOpen())
        {
            ToggleMainUI();
        }
    }

    public void CloseMainUI()
    {
        if (IsMainUIOpen())
        {
            ToggleMainUI();
        }
    }

    public void AddWindow(OcelotWindow window)
    {
        if (!windows.Contains(window))
        {
            manager.AddWindow(window);
            windows.Add(window);
        }
    }

    public bool HasWindow<T>() where T : OcelotWindow
    {
        return windows.OfType<T>().Any();
    }

    public T GetWindow<T>() where T : OcelotWindow
    {
        var window = windows.OfType<T>().FirstOrDefault();
        if (window == null)
        {
            throw new UnableToLoadWindowException($"Window of type {typeof(T).Name} was not found.");
        }

        return window;
    }

    public bool TryGetWindow<T>(out T? window) where T : OcelotWindow
    {
        try
        {
            window = GetWindow<T>();
            return true;
        }
        catch (UnableToLoadWindowException ex)
        {
            Logger.Error(ex.Message);
            window = null;
            return false;
        }
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= manager.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;

        manager.RemoveAllWindows();

        foreach (var window in windows)
        {
            window.Dispose();
        }

        windows.Clear();
    }
}
