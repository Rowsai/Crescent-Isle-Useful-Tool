using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace Ocelot.Config.Handlers;

public abstract class MultiSelectHandler<T>(ModuleConfig self, ConfigAttribute attribute, PropertyInfo prop) : Handler(self, attribute, prop)
    where T : notnull
{
    protected override Type type
    {
        get => typeof(List<T>);
    }

    private readonly bool isSearchable = prop.IsDefined(typeof(SearchableAttribute), true);

    private string searchTerm = "";

    protected override (bool handled, bool changed) RenderComponent(RenderContext context)
    {
        var currentSelectedValues = GetValue(context);

        var dirty = false;

        var previewLabel = currentSelectedValues.Any() ? string.Join(", ", currentSelectedValues.Select(GetLabel)) : "None selected";

        if (ImGui.BeginCombo(context.GetLabelWithId(), previewLabel, ImGuiComboFlags.HeightLarge))
        {
            if (isSearchable)
            {
                ImGui.InputText("##search", ref searchTerm, 256);
                ImGui.Separator();
            }

            var values = GetValuesToShow().ToList();
            var selected = values.Where(v => currentSelectedValues.Contains(v));
            var unselected = values.Where(v => !currentSelectedValues.Contains(v));
            values = selected.Concat(unselected).ToList();

            var height = Math.Min(512, values.Count * ImGui.GetTextLineHeightWithSpacing());

            using (ImRaii.Child("##options", new Vector2(0, height)))
            {
                foreach (var value in values)
                {
                    var isSelected = currentSelectedValues.Contains(value);
                    if (ImGui.Selectable(GetLabel(value), isSelected))
                    {
                        if (isSelected)
                        {
                            currentSelectedValues.Remove(value); // Deselect
                        }
                        else
                        {
                            currentSelectedValues.Add(value); // Select
                        }

                        // Update the property with the modified list
                        SetValue(context, currentSelectedValues);
                        dirty = true;
                    }
                }
            }

            ImGui.EndCombo();
        }

        return (true, dirty);
    }

    private IEnumerable<T> GetValuesToShow()
    {
        foreach (var value in GetData())
        {
            if (!Filter(value) || !SearchFilter(value))
            {
                continue;
            }

            yield return value;
        }
    }

    private bool SearchFilter(T item)
    {
        if (!isSearchable || searchTerm.Trim() == string.Empty)
        {
            return true;
        }

        return GetLabel(item).Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    protected virtual List<T> GetValue(RenderContext context)
    {
        return context.GetValue() as List<T> ?? [];
    }

    protected virtual void SetValue(RenderContext context, List<T> value)
    {
        context.SetValue(value);
    }

    protected abstract IEnumerable<T> GetData();

    protected abstract bool Filter(T item);

    protected abstract string GetLabel(T item);
}
