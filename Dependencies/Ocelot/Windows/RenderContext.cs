using System.Numerics;
using ECommons.GameHelpers;
using Dalamud.Bindings.ImGui;
using Ocelot.Commands;
using Ocelot.IPC;
using Ocelot.Modules;
using Pictomancy;

namespace Ocelot.Windows;

public class RenderContext(OcelotPlugin plugin, IModule? module = null)
{
    public readonly OcelotPlugin Plugin = plugin;

    public IModule? Module { get; private set; } = module;

    public readonly PctDrawList Pictomancy = PctService.GetDrawList();

    public IOcelotConfig Config
    {
        get => Plugin.OcelotConfig;
    }

    public ModuleManager Modules
    {
        get => Plugin.Modules;
    }

    public WindowManager Windows
    {
        get => Plugin.Windows;
    }

    public CommandManager Commands
    {
        get => Plugin.Commands;
    }

    public IPCManager IPC
    {
        get => Plugin.IPC;
    }

    public RenderContext ForModule(IModule module)
    {
        return new RenderContext(Plugin, module);
    }

    public bool IsForModule<T>(out T module) where T : class, IModule
    {
        if (Module is T typed)
        {
            module = typed;
            return true;
        }

        module = null!;
        return false;
    }

    public void DrawLine(Vector3 start, Vector3 end, Vector4 color, float thickness = 3f)
    {
        Pictomancy.AddLine(start, end, 1f / 1000f, ImGui.GetColorU32(color), thickness);
    }

    public void DrawLine(Vector3 end, Vector4 color, float thickness = 3f)
    {
        DrawLine(Player.Position, end, color, thickness);
    }

    public enum CircleDrawMode
    {
        Outline,

        Filled,
    }

    public void DrawCircle(Vector3 position, float radius, Vector4 color, CircleDrawMode mode = CircleDrawMode.Outline)
    {
        if (mode == CircleDrawMode.Outline)
        {
            Pictomancy.AddCircle(position, radius, ImGui.GetColorU32(color));
        }

        if (mode == CircleDrawMode.Filled)
        {
            Pictomancy.AddCircleFilled(position, radius, ImGui.GetColorU32(color));
        }
    }
}
