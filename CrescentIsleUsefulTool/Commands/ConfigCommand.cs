using System.Collections.Generic;
using Ocelot.Commands;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Commands;

[OcelotCommand]
public class ConfigCommand(Plugin plugin) : OcelotCommand
{
    protected override string Command
    {
        get => "/ciutcfg";
    }

    protected override string Description
    {
        get => @"
設定画面を開きます。
 - /ciutcfg : 設定画面を開く
 - /ciut config : メインコマンドから設定画面を開く
--------------------------------
".Trim();
    }

    protected override IReadOnlyList<string> Aliases
    {
        get => ["/ciutconfig", "/crescentconfig"];
    }


    public override void Execute(string command, string arguments)
    {
        plugin.Windows.ToggleConfigUI();
    }
}
