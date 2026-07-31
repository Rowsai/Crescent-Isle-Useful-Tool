using System.Collections.Generic;
using CrescentIsleUsefulTool.Modules.Automator;
using Ocelot.Commands;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Commands;

[OcelotCommand]
public class IllegalModeCommand(Plugin plugin) : OcelotCommand
{
    protected override string Command
    {
        get => "/ciutillegal";
    }

    protected override string Description
    {
        get => @"
不正モードの画面と稼働状態を操作します。
 - /ciutillegal : 不正モード画面を開閉
 - /ciutillegal on : 不正モードを有効化
 - /ciutillegal off : 不正モードを無効化
 - /ciutillegal toggle : 不正モードのON/OFFを切り替え
--------------------------------
".Trim();
    }

    protected override IReadOnlyList<string> Aliases
    {
        get => ["/crescentillegal"];
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
                automator.EnableIllegalMode();
                break;
            case "off":
                automator.DisableIllegalMode();
                break;
            case "toggle":
                AutomatorModule.ToggleIllegalMode(plugin);
                break;
        }

        plugin.Config.Save();
    }
}
