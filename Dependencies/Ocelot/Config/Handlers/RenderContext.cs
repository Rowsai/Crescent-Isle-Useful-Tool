using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Interface;
using ECommons.Reflection;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace Ocelot.Config.Handlers;

public class RenderContext
{
    private readonly PropertyInfo prop;

    private readonly Type type;

    private readonly ModuleConfig self;

    public RenderContext(PropertyInfo prop, Type type, ModuleConfig self)
    {
        this.prop = prop;
        this.type = type;
        this.self = self;
    }


    public bool IsValid()
    {
        return prop.PropertyType == type;
    }

    public bool ShouldRender()
    {
        if (HasUnloadedRequiredPlugins(out _))
        {
            return false;
        }

        if (HasLoadedConflictingPlugins(out _))
        {
            return false;
        }

        var render = prop.GetCustomAttribute<DependsOnAttribute>();
        if (render == null)
        {
            return true;
        }

        foreach (var dependency in render.dependencies)
        {
            var dependent = self.GetType().GetProperty(dependency);
            if (dependent == null || dependent.PropertyType != typeof(bool))
            {
                return false;
            }

            if (!(bool)(dependent.GetValue(self) ?? false))
            {
                return false;
            }
        }

        return true;
    }

    public bool HasUnloadedRequiredPlugins(out List<string> unloaded)
    {
        unloaded = [];

        var attr = prop.GetCustomAttribute<RequiredPluginAttribute>();
        if (attr == null)
        {
            return false;
        }

        foreach (var plugin in attr.dependencies)
        {
            if (!DalamudReflector.TryGetDalamudPlugin(plugin, out _, false, true))
            {
                unloaded.Add(plugin);
            }
        }

        return unloaded.Count > 0;
    }


    public bool HasLoadedConflictingPlugins(out List<string> loaded)
    {
        loaded = [];

        var attr = prop.GetCustomAttribute<ConflictingPluginAttribute>();
        if (attr == null)
        {
            return false;
        }

        foreach (var plugin in attr.conflicts)
        {
            if (DalamudReflector.TryGetDalamudPlugin(plugin, out _, false, true))
            {
                loaded.Add(plugin);
            }
        }

        return loaded.Count > 0;
    }

    public void CustomIcons()
    {
        var iconAttr = prop.GetCustomAttribute<IconAttribute>();
        if (iconAttr == null)
        {
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.Text, iconAttr.color);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(iconAttr.icon.ToIconString());
        ImGui.PopFont();
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(I18N.T(iconAttr.tooltip_translation_key));
        }

        ImGui.SameLine();
    }


    public bool IsExperimental()
    {
        return prop.GetCustomAttribute<ExperimentalAttribute>() != null;
    }

    public uint GetIndentation()
    {
        return prop.GetCustomAttribute<IndentAttribute>()?.depth ?? 0;
    }

    public void Experimental()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.9f, 0.0f, 1.0f));
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(FontAwesomeIcon.ExclamationTriangle.ToIconString());
        ImGui.PopFont();
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Experimental");
        }

        ImGui.SameLine();
    }

    public bool IsIllegal()
    {
        return prop.GetCustomAttribute<IllegalAttribute>() != null;
    }

    public void Illegal()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(FontAwesomeIcon.ExclamationTriangle.ToIconString());
        ImGui.PopFont();
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("May lead to a ban. Use at your own risk.");
        }

        ImGui.SameLine();
    }

    public void Tooltip()
    {
        string? translation_key = null;
        translation_key ??= prop.GetCustomAttribute<TooltipAttribute>()?.translation_key;
        translation_key ??= prop.GetCustomAttribute<LabelAndTooltipAttribute>()?.translation_key;

        var hasLabel = prop.GetCustomAttribute<LabelAttribute>() != null;

        if (ImGui.IsItemHovered())
        {
            if (translation_key == null && !hasLabel)
            {
                ImGui.SetTooltip(self.Owner.T($"config.{ToSnakeCase(prop.Name)}.tooltip"));
                return;
            }

            if (translation_key != null && !hasLabel)
            {
                ImGui.SetTooltip(I18N.T(translation_key));
            }
        }
    }

    public string GetLabel()
    {
        string? translation_key = null;
        translation_key ??= prop.GetCustomAttribute<LabelAttribute>()?.translation_key;
        translation_key ??= prop.GetCustomAttribute<LabelAndTooltipAttribute>()?.translation_key;
        if (translation_key != null)
        {
            return I18N.T(translation_key);
        }

        if (self.Owner == null)
        {
            return "Unknown label";
        }

        return self.Owner.T($"config.{ToSnakeCase(prop.Name)}.label");
    }

    public string GetLabelWithId()
    {
        return $"{GetLabel()}##{prop.GetHashCode()}";
    }

    public object? GetValue()
    {
        return prop.GetValue(self);
    }

    public void SetValue(object? value)
    {
        prop.SetValue(self, value);
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                // Add underscore if it's not the first character AND
                // (the previous character is lowercase OR the next character is lowercase)
                if (i > 0 && (char.IsLower(input[i - 1]) || i + 1 < input.Length && char.IsLower(input[i + 1])))
                {
                    result.Append('_');
                }

                result.Append(char.ToLower(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
