using System.Linq;
using System.Numerics;
using BOCCHI.Modules;
using BOCCHI.Ui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Ocelot.Modules;
using Ocelot.Windows;

namespace BOCCHI.Windows;

[OcelotConfigWindow]
public class ConfigWindow(Plugin primaryPlugin, Config config) : OcelotConfigWindow(primaryPlugin, config)
{
    private IModule? selectedConfigModule;

    public override void PostInitialize()
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 0),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    protected override void Render(RenderContext context)
    {
        using var theme = CrescentTheme.Push();
        var modules = Plugin.Modules.GetModulesByConfigOrder().ToList();
        selectedConfigModule ??= modules.FirstOrDefault();

        using (ImRaii.Child("##LeftPanel", new Vector2(300, 0), true))
        {
            ImGui.TextColored(CrescentTheme.AccentSoft, "MODULES");
            ImGui.TextDisabled("Crescent Isle Useful Tool");
            ImGui.Separator();

            foreach (var module in modules)
            {
                if (module is not Module concreteModule || concreteModule.Config == null)
                {
                    continue;
                }

                var name = concreteModule.Config.GetType().Name;

                var title = concreteModule.Config.GetTitle();
                if (title != null)
                {
                    name = title;
                }

                var selected = module == selectedConfigModule;
                if (ImGui.Selectable(name, selected))
                {
                    selectedConfigModule = module;
                }
            }
        }

        ImGui.SameLine();

        var activeModule = selectedConfigModule;
        if (activeModule == null)
        {
            ImGui.TextDisabled("No configurable modules are available.");
            return;
        }

        using (ImRaii.Child("##RightPanel", new Vector2(0, 0), true))
        {
            var settingsTitle = activeModule is Module selectedModule
                ? selectedModule.Config?.GetTitle()
                : null;
            ImGui.TextColored(CrescentTheme.AccentSoft, settingsTitle ?? "SETTINGS");
            ImGui.Separator();
            activeModule.RenderConfigUi(context);
        }
    }
}
