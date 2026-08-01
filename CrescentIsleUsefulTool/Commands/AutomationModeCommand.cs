using System.Collections.Generic;
using CrescentIsleUsefulTool.Modules.Automator;
using Ocelot.Commands;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Commands;

[OcelotCommand]
public class AutomationModeCommand(Plugin plugin) : OcelotCommand
{
    protected override string Command
    {
        get => "/ciutauto";
    }

    protected override string Description
    {
        get => @"
自動操作モードの画面と稼働状態を操作します。
 - /ciutauto : 自動操作モード画面を開閉
 - /ciutauto on : 自動操作モードを有効化
 - /ciutauto off : 自動操作モードを無効化
 - /ciutauto toggle : 自動操作モードのON/OFFを切り替え
--------------------------------
".Trim();
    }

    protected override IReadOnlyList<string> Aliases
    {
        get => ["/crescentauto"];
    }

    protected override IReadOnlyList<string> ValidArguments
    {
        get => ["on", "off", "toggle"];
    }

    public override void Execute(string command, string arguments)
    {
        if (arguments.Trim() == "")
        {
            plugin.Windows.GetWindow<AutomatorWindow>()?.Toggle();
            return;
        }

        if (!plugin.Modules.TryGetModule<AutomatorModule>(out var automator) || automator == null)
        {
            return;
        }

        switch (arguments)
        {
            case "on":
                automator.EnableAutomationMode();
                break;
            case "off":
                automator.DisableAutomationMode();
                break;
            case "toggle":
                AutomatorModule.ToggleAutomationMode(plugin);
                break;
        }

        plugin.Config.Save();
    }
}
