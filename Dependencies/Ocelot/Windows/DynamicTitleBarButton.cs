using System;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace Ocelot.Windows;

public class DynamicTitleBarButton : TitleBarButton
{
    private Action<DynamicTitleBarButton, ImGuiMouseButton> OnClick;

    public DynamicTitleBarButton(Action<DynamicTitleBarButton, ImGuiMouseButton> OnClick)
    {
        this.OnClick = OnClick;
        Click = m => OnClick(this, m);
    }
}
