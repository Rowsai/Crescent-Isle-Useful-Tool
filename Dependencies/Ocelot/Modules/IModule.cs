using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Ocelot.Data;
using Ocelot.IPC;
using Ocelot.Windows;

namespace Ocelot.Modules;

public interface IModule : IDisposable
{
    bool HasRequiredIPCs { get; }

    bool IsEnabled { get; }

    bool ShouldUpdate { get; }

    bool ShouldRender { get; }

    bool ShouldInitialize { get; }

    ModuleConfig? Config { get; }

    void PreInitialize();

    void Initialize();

    void PostInitialize();

    void InjectModules();

    void InjectIPCs();

    UpdateLimit UpdateLimit { get; }

    // Functions
    void PreUpdate(UpdateContext context);

    void Update(UpdateContext context);

    void PostUpdate(UpdateContext context);

    void Render(RenderContext context);

    bool RenderMainUi(RenderContext context);

    void RenderConfigUi(RenderContext context);

    // Events
    void OnChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, bool isHandled);

    void OnTerritoryChanged(uint id);

    void Debug(string log);

    void Error(string log);

    void Fatal(string log);

    void Info(string log);

    void Verbose(string log);

    void Warning(string log);

    string Translate(string key);

    string Trans(string key);

    string T(string key);

    T GetModule<T>() where T : class, IModule;

    bool TryGetModule<T>(out T? module) where T : class, IModule;

    T GetIPCSubscriber<T>() where T : IPCSubscriber;

    bool TryGetIPCSubscriber<T>(out T? provider) where T : IPCSubscriber;

    T GetWindow<T>() where T : OcelotWindow;

    bool TryGetWindow<T>(out T? provider) where T : OcelotWindow;
}
