using System.Collections.Generic;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ipc;
using ECommons.DalamudServices;
using Ocelot;
using Ocelot.IPC;
using Ocelot.Modules;
using Ocelot.Windows;

namespace CrescentIsleUsefulTool.Modules.Automator;

[OcelotModule(int.MaxValue - 1)]
public class AutomatorModule : Module
{
    public override AutomatorConfig Config
    {
        get => PluginConfig.AutomatorConfig;
    }

    public override bool IsEnabled
    {
        // Keep the module updating while dependencies are still starting so
        // illegal mode can remain ON and resume automatically when IPC is ready.
        get => Config.Enabled;
    }

    public readonly Automator automator = new();

    public readonly Panel panel = new();

    private readonly List<uint> occultCrescentTerritoryIds = [ZoneData.SOUTHHORN, ZoneData.NORTHHORN];

    public AutomatorModule(Plugin plugin, Config config)
        : base(plugin, config)
    {
        config.AutomatorConfig.Enabled = false;
        config.Save();
    }


    public override void PostUpdate(UpdateContext context)
    {
        automator.PostUpdate(this, context.Framework);
    }


    public override bool RenderMainUi(RenderContext context)
    {
        panel.Draw(this);
        return true;
    }

    public override void OnTerritoryChanged(uint id)
    {
        if (occultCrescentTerritoryIds.Contains(id))
        {
            return;
        }

        automator.Refresh();
        Config.Enabled = false;
        PluginConfig.Save();
    }

    public static void ToggleIllegalMode(OcelotPlugin plugin)
    {
        var module = plugin.Modules.GetModule<AutomatorModule>();
        if (!module.Config.Enabled)
        {
            module.EnableIllegalMode();
        }
        else
        {
            module.DisableIllegalMode();
        }
    }

    public void EnableIllegalMode()
    {
        var wasDisabled = !Config.Enabled;
        Config.Enabled = true;
        automator.Refresh();
        automator.SetRuntimeStatus("FATE／CEと移動プラグインの状態を確認しています。");
        PluginConfig.Save();

        if (wasDisabled)
        {
            Svc.Chat.Print(T("messages.on"));
        }
    }

    public void DisableIllegalMode()
    {
        var wasEnabled = Config.Enabled;
        Config.Enabled = false;
        automator.Refresh();
        automator.SetRuntimeStatus("停止中");
        TryGetIPCSubscriber<VNavmesh>(out var vnav);
        VnavmeshIpc.TryStop(vnav);
        Plugin.Chain.Abort();
        if (Config.ShouldToggleAiProvider)
        {
            Config.AiProvider.Off();
        }

        if (wasEnabled)
        {
            Svc.Chat.Print(T("messages.off"));
        }

        PluginConfig.Save();
    }
}
