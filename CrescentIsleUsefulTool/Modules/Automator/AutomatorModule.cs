using System.Collections.Generic;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.Treasure;
using ECommons.DalamudServices;
using Ocelot;
using Ocelot.IPC;
using Ocelot.Modules;
using Ocelot.Windows;
using CrescentIsleUsefulTool.Enums;

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
        // Automation mode can remain ON and resume automatically when IPC is ready.
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

    public static void ToggleAutomationMode(OcelotPlugin plugin)
    {
        var module = plugin.Modules.GetModule<AutomatorModule>();
        if (!module.Config.Enabled)
        {
            module.EnableAutomationMode();
        }
        else
        {
            module.DisableAutomationMode();
        }
    }

    public void EnableAutomationMode()
    {
        var wasDisabled = !Config.Enabled;
        Config.Enabled = true;
        if (TryGetModule<TreasureModule>(out var treasure) && treasure?.Config.Enabled == true)
        {
            // The treasure automation switch follows the main automation
            // switch. Users can stop both from the single main-window button.
            treasure.Config.EnableTreasureHunt = true;
        }

        automator.Refresh();
        automator.SetRuntimeStatus("FATE／CEと移動プラグインの状態を確認しています。");
        PluginConfig.Save();

        if (wasDisabled)
        {
            if (TryRunAutomatedTreasureHunt(runStartupPreparation: true))
            {
                automator.SetRuntimeStatus("宝箱自動モードを開始し、拠点で準備しています。");
            }
            else
            {
                Plugin.Chain.Abort();
                Plugin.Chain.Submit(ChainHelper.TankyushinAtKnowledgeCrystalChain());
                automator.SetRuntimeStatus("ナレッジクリスタルへ移動し、たんきゅうしんを使用します。");
            }

            Svc.Chat.Print(T("messages.on"));
        }
    }

    public void DisableAutomationMode()
    {
        var wasEnabled = Config.Enabled;
        Config.Enabled = false;
        automator.Refresh();
        automator.SetRuntimeStatus("停止中");
        TryGetIPCSubscriber<VNavmesh>(out var vnav);
        VnavmeshIpc.TryStop(vnav);
        Plugin.Chain.Abort();
        PauseAutomatedTreasureHunt("自動操作モードを停止したため、宝箱巡回を一時停止しました。");
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

    internal bool TryRunAutomatedTreasureHunt(bool runStartupPreparation = false)
    {
        if (!Config.Enabled ||
            !ZoneData.IsInOccultCrescent() ||
            !TryGetModule<TreasureModule>(out var treasure) ||
            treasure == null ||
            !treasure.Config.ShouldEnableTreasureHunt)
        {
            return false;
        }

        return treasure.Hunter.StartForAutomation(runStartupPreparation);
    }

    internal void PauseAutomatedTreasureHunt(string reason)
    {
        if (TryGetModule<TreasureModule>(out var treasure) && treasure?.Hunter.IsRunning == true)
        {
            treasure.Hunter.PauseForConflictingMode(reason);
        }
    }

    internal bool IsAutomatedTreasureHuntRunning()
    {
        return TryGetModule<TreasureModule>(out var treasure) && treasure?.Hunter.IsRunning == true;
    }

    public void SetCriticalEncounterTravelEnabled(bool enabled)
    {
        Config.DoCriticalEncounters = enabled;
        if (!enabled && automator.Activity?.data.Type == EventType.CriticalEncounter)
        {
            StopCurrentActivity();
        }

        PluginConfig.Save();
    }

    public void SetFateTravelEnabled(bool enabled)
    {
        Config.DoFates = enabled;
        if (!enabled && automator.Activity?.data.Type == EventType.Fate)
        {
            StopCurrentActivity();
        }

        PluginConfig.Save();
    }

    private void StopCurrentActivity()
    {
        automator.Refresh();
        TryGetIPCSubscriber<VNavmesh>(out var vnav);
        VnavmeshIpc.TryStop(vnav);
        Plugin.Chain.Abort();
        if (Config.ShouldToggleAiProvider)
        {
            Config.AiProvider.Off();
        }
    }
}
