using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Bindings.ImGui;

namespace Ocelot;

public static class OcelotColor
{
    public static Vector4 Text { get; } = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];

    public static Vector4 Disabled { get; } = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

    public static Vector4 Yellow { get; } = new(1f, 0.75f, 0.25f, 1f);

    public static Vector4 Blue { get; } = ImGuiColors.ParsedBlue;

    public static Vector4 Error { get; } = new(0.89f, 0.29f, 0.29f, 1f);
}
