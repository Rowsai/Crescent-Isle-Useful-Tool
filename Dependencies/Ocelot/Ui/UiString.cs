using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;

namespace Ocelot.Ui;

public interface UiStringComponent
{
    float GetWidth();

    bool Render();
}

public class UiTextComponent(string text, Vector4 color, ImFontPtr font) : UiStringComponent
{
    public ImFontPtr Font { get; private set; } = font;

    public string Text { get; private set; } = text;

    public Vector4 Color { get; private set; } = color;

    public UiTextComponent(string text)
        : this(text, Vector4.Zero, UiBuilder.DefaultFont)
    {
        Color = OcelotColor.Text;
    }

    public UiTextComponent(string text, Vector4 color)
        : this(text, color, UiBuilder.DefaultFont)
    {
    }

    public UiTextComponent(string text, ImFontPtr font)
        : this(text, Vector4.Zero, font)
    {
        Color = OcelotColor.Text;
    }

    public float GetWidth()
    {
        using var font = ImRaii.PushFont(Font);
        return ImGui.CalcTextSize(Text).X;
    }

    public bool Render()
    {
        using var font = ImRaii.PushFont(Font);
        ImGui.TextColored(Color, Text);
        return ImGui.IsItemHovered();
    }

    public UiTextComponent WithText(string text)
    {
        Text = text;
        return this;
    }

    public UiTextComponent WithColor(Vector4 color)
    {
        Color = color;
        return this;
    }

    public UiTextComponent WithFont(ImFontPtr font)
    {
        Font = font;
        return this;
    }
}

public class UiImageComponent : UiStringComponent
{
    private IDalamudTextureWrap texture;

    public UiImageComponent(IDalamudTextureWrap texture)
    {
        this.texture = texture;
    }

    public UiImageComponent(uint id)
    {
        texture = Svc.Texture.GetFromGameIcon(new GameIconLookup(id)).GetWrapOrEmpty();
    }

    public float GetHeight()
    {
        return ImGui.GetFontSize();
    }


    public float GetWidth()
    {
        var aspect = (float)texture.Width / texture.Height;
        return GetHeight() * aspect;
    }

    public bool Render()
    {
        ImGui.Image(texture.Handle, new Vector2(GetWidth(), GetHeight()));
        return ImGui.IsItemHovered();
    }
}

public class UiString
{
    public readonly List<UiStringComponent> Components = [];

    public UiString()
    {
    }

    public UiString(IEnumerable<UiStringComponent> components)
    {
        Components.AddRange(components);
    }

    public static UiString Text(string text, Vector4? color = null, ImFontPtr? font = null)
    {
        var comp = new UiTextComponent(text);
        if (color is { } c)
        {
            comp.WithColor(c);
        }

        if (font is { } f)
        {
            comp.WithFont(f);
        }

        return new UiString().Add(comp);
    }

    public UiString Add(string text)
    {
        return Add(new UiTextComponent(text));
    }

    public UiString Add(string text, Vector4 color)
    {
        return Add(new UiTextComponent(text, color));
    }

    public UiString Add(string text, ImFontPtr font)
    {
        return Add(new UiTextComponent(text, font));
    }

    public UiString Add(FontAwesomeIcon icon, Vector4 color)
    {
        return Add(new UiTextComponent(icon.ToString(), color, UiBuilder.IconFont));
    }

    public UiString Add(FontAwesomeIcon icon)
    {
        return Add(new UiTextComponent(icon.ToString(), UiBuilder.IconFont));
    }

    public UiString Add(UiStringComponent component)
    {
        Components.Add(component);
        return this;
    }

    public UiString AddImage(IDalamudTextureWrap texture)
    {
        return Add(new UiImageComponent(texture));
    }

    public UiString AddIcon(uint iconId)
    {
        return Add(new UiImageComponent(iconId));
    }

    public float Width
    {
        get => Components.Count == 0 ? 0 : Components.Sum(c => c.GetWidth()) + ImGui.GetStyle().ItemSpacing.X * (Components.Count - 1);
    }

    public UiState Render()
    {
        var hovered = false;
        for (var i = 0; i < Components.Count; i++)
        {
            if (Components[i].Render())
            {
                hovered = true;
            }

            if (i < Components.Count - 1)
            {
                ImGui.SameLine();
            }
        }

        return hovered ? UiState.Hovered : UiState.None;
    }
}
