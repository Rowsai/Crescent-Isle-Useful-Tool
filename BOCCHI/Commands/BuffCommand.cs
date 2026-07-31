using System.Collections.Generic;
using BOCCHI.Modules.Buff;
using Ocelot.Commands;
using Ocelot.Modules;

namespace BOCCHI.Commands;

[OcelotCommand]
public class BuffCommand(Plugin plugin) : OcelotCommand
{
    protected override string Command
    {
        get => "/ciutbuff";
    }

    protected override string Description
    {
        get => "ナレッジバフの更新処理を実行します。";
    }

    protected override IReadOnlyList<string> Aliases
    {
        get => ["/crescentbuff"];
    }

    public override void Execute(string command, string arguments)
    {
        plugin.Modules.GetModule<BuffModule>().BuffManager.QueueBuffs();
    }
}
