using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ECommons;
using Ocelot.Chain;
using Ocelot.Commands;
using Ocelot.Data;
using Ocelot.Debug;
using Ocelot.IPC;
using Ocelot.Modules;
using Ocelot.Windows;
using Pictomancy;
using System;

namespace Ocelot;

public abstract class OcelotPlugin : IDalamudPlugin
{
    public abstract string Name { get; }

    public virtual string Version
    {
        get => Svc.PluginInterface.Manifest.AssemblyVersion.ToString();
    }

    public const string OcelotVersion = "1.1.5";

    public abstract IOcelotConfig OcelotConfig { get; }

    public readonly ModuleManager Modules = new();

    public readonly WindowManager Windows = new();

    public readonly CommandManager Commands = new();

    public readonly IPCManager IPC = new();

    protected readonly PluginWatcher PluginWatcher;

    public RenderContext? RenderContext { get; private set; } = null;

    protected OcelotPlugin(IDalamudPluginInterface plugin, params Module[] eModules)
    {
        ECommonsMain.Init(plugin, this, eModules);
        PctService.Initialize(plugin);

        Registry.RegisterAssemblies(typeof(OcelotPlugin).Assembly);
        Registry.RegisterAssemblies(GetType().Assembly);

        PluginWatcher = new PluginWatcher();
    }

    protected void OcelotInitialize(params OcelotFeature[] features)
    {
        Svc.PluginInterface.UiBuilder.Draw += PreRender;

        OcelotFeatureEx.SetFeatures(features.Length <= 0 ? [OcelotFeature.All] : features);

        if (OcelotFeature.IPC.IsEnabled())
        {
            Logger.Info("Initializing IPC Manager...");
            IPC.Initialize();
        }

        if (OcelotFeature.ModuleManager.IsEnabled())
        {
            Logger.Info("Initializing Module Manager...");
            Modules.AutoRegister(this, OcelotConfig);
            Modules.PreInitialize();
            Modules.Initialize();
            Svc.PluginInterface.UiBuilder.Draw += Render;
        }

        if (OcelotFeature.WindowManager.IsEnabled())
        {
            Logger.Info("Initializing Window Manager...");
            Windows.Initialize(this, OcelotConfig);
        }

        if (OcelotFeature.CommandManager.IsEnabled())
        {
            Logger.Info("Initializing Command Manager...");
            Commands.Initialize(this);
        }

        if (OcelotFeature.Prowler.IsEnabled() && OcelotFeature.IPC.IsEnabled())
        {
            Logger.Info("Initializing Prowler...");
            Prowler.Prowler.Initialize(this);
        }

        if (OcelotFeature.ChainManager.IsEnabled())
        {
            ChainManager.Initialize();
        }

        Modules.PostInitialize();
        Modules.InjectModules();
        Modules.InjectIPCs();

        Svc.Framework.Update += Update;
        Svc.Chat.ChatMessage += OnChatMessage;
        Svc.ClientState.TerritoryChanged += OnTerritoryChanged;

        Svc.PluginInterface.UiBuilder.Draw += PostRender;

        PluginWatcher.OnPluginListChanged += OnPluginListChanged;

        if (PluginWatcher.IsOcelotPluginEnabled(OcelotPlugins.OcelotMonitor))
        {
            IPC.AddProvider(new DebugIPCProvider(this));
        }
    }

    protected virtual bool ShouldUpdate()
    {
        return true;
    }

    protected virtual void Update(IFramework framework)
    {
        if (!ShouldUpdate())
        {
            return;
        }

        var context = new UpdateContext(framework, this);

        PluginWatcher.Update(context);

        Modules.PreUpdate(context);
        Modules.Update(context);
        Modules.PostUpdate(context);
    }

    protected virtual void Render()
    {
        if (RenderContext == null)
        {
            return;
        }

        Modules.Render(RenderContext);
    }

    protected virtual void OnChatMessage(IHandleableChatMessage message)
    {
        Modules.OnChatMessage(message.LogKind, message.Timestamp, message.Sender, message.Message, message.IsHandled);
    }

    protected virtual void OnTerritoryChanged(uint id)
    {
        Modules.OnTerritoryChanged(id);
    }

    private void PreRender()
    {
        var draw = PctService.Draw();
        if (draw == null)
        {
            RenderContext = null;
            return;
        }

        RenderContext = new RenderContext(this);
    }

    private void PostRender()
    {
        if (RenderContext == null)
        {
            return;
        }

        try
        {
            PctService.GetDrawList().Dispose();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnPluginListChanged()
    {
        if (OcelotFeature.IPC.IsEnabled())
        {
            IPC.Initialize();
        }

        Modules.InjectIPCs();
    }

    public virtual void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= PostRender;
        Svc.PluginInterface.UiBuilder.Draw -= Render;
        Svc.PluginInterface.UiBuilder.Draw -= PreRender;

        PluginWatcher.OnPluginListChanged -= OnPluginListChanged;

        Modules.Dispose();
        Windows.Dispose();
        IPC.Dispose();
        Commands.Dispose();
        ChainManager.Close();

        Svc.Framework.Update -= Update;
        Svc.Chat.ChatMessage -= OnChatMessage;
        Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;

        PctService.Dispose();
        ECommonsMain.Dispose();
    }
}
