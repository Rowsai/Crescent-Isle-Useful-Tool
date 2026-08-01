using System.Numerics;
using CrescentIsleUsefulTool.Ipc;
using Dalamud.Bindings.ImGui;
using Ocelot.Ui;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Modules.Debug.Panels;

public class VnavmeshPanel : Panel
{
    public override string GetName()
    {
        return "Vnavmesh";
    }

    public override void Render(DebugModule module)
    {
        if (module.TryGetIPCSubscriber<VNavmesh>(out var vnav) && VnavmeshIpc.IsOperational(vnav, out _))
        {
            OcelotUi.Title("Vnav state:");
            ImGui.SameLine();
            VnavmeshIpc.TryIsRunning(vnav, out var isRunning);
            ImGui.TextUnformatted(isRunning ? "Running" : "Pending");


            if (ImGui.Button("Test vnav thingy"))
            {
                VnavmeshIpc.TryFollowPath(vnav, [new Vector3(815.2f, 72.5f, -705.15f)], false);
            }
        }
    }
}
