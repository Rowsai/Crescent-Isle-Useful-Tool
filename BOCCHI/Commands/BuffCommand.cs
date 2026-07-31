using BOCCHI.Modules.Buff;
using Ocelot.Commands;
using Ocelot.Modules;

namespace BOCCHI.Commands;

[OcelotCommand]
public class BuffCommand(Plugin plugin) : OcelotCommand
{
    protected override string Command
    {
        get => "/bocchibuff";
    }

    protected override string Description
    {
        get => "";
    }


    public override void Execute(string command, string arguments)
    {
        plugin.Modules.GetModule<BuffModule>().BuffManager.QueueBuffs();
    }
}
