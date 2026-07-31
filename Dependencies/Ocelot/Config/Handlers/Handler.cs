using System;
using System.Collections.Generic;
using System.Reflection;
using Ocelot.Config.Attributes;
using Ocelot.Modules;
using Ocelot.Ui;

namespace Ocelot.Config.Handlers;

public abstract class Handler(ModuleConfig self, ConfigAttribute attribute, PropertyInfo property)
{
    protected abstract Type type { get; }

    protected readonly ConfigAttribute attribute = attribute;

    protected readonly PropertyInfo property = property;

    private RenderContext GetContext()
    {
        return new RenderContext(property, type, self);
    }

    public bool HasUnloadedRequiredPlugins(out List<string> unloaded)
    {
        return GetContext().HasUnloadedRequiredPlugins(out unloaded);
    }

    public bool HasLoadedConflictingPlugins(out List<string> loaded)
    {
        return GetContext().HasLoadedConflictingPlugins(out loaded);
    }

    public (bool handled, bool changed) Render()
    {
        var context = GetContext();

        if (!context.IsValid() || !context.ShouldRender())
        {
            return (false, false);
        }

        var handled = false;
        var changed = false;
        OcelotUi.Indent(context.GetIndentation(), () =>
        {
            context.CustomIcons();

            if (context.IsIllegal())
            {
                context.Illegal();
            }

            if (context.IsExperimental())
            {
                context.Experimental();
            }

            (handled, changed) = RenderComponent(context);

            context.Tooltip();
        });

        return (handled, changed);
    }

    protected abstract (bool handled, bool changed) RenderComponent(RenderContext payload);
}
