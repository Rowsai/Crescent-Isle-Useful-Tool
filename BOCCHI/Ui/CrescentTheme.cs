using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BOCCHI.Ui;

/// <summary>
/// A compact, plugin-local blue theme. It is scoped to this plugin's window
/// contents and therefore does not alter the user's global Dalamud theme.
/// </summary>
public static class CrescentTheme
{
    public static readonly Vector4 Accent = new(0.18f, 0.66f, 1.00f, 1.00f);

    public static readonly Vector4 AccentSoft = new(0.38f, 0.79f, 1.00f, 1.00f);

    public static ThemeScope Push()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.88f, 0.94f, 1.00f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, new Vector4(0.48f, 0.64f, 0.80f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.018f, 0.055f, 0.115f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.025f, 0.070f, 0.145f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.12f, 0.42f, 0.72f, 0.75f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.12f, 0.40f, 0.72f, 0.65f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.045f, 0.125f, 0.235f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.075f, 0.235f, 0.420f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.095f, 0.310f, 0.560f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.055f, 0.205f, 0.390f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.090f, 0.360f, 0.650f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.120f, 0.500f, 0.860f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.055f, 0.205f, 0.390f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.090f, 0.360f, 0.650f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.120f, 0.500f, 0.860f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.030f, 0.105f, 0.210f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.075f, 0.300f, 0.560f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.080f, 0.390f, 0.720f, 1.00f));

        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 7f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 7f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f));

        return new ThemeScope();
    }

    public readonly struct ThemeScope : IDisposable
    {
        public void Dispose()
        {
            ImGui.PopStyleVar(6);
            ImGui.PopStyleColor(18);
        }
    }
}
