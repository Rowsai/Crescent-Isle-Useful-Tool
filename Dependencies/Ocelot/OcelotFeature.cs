using System.Collections.Generic;
using ECommons;

namespace Ocelot;

public enum OcelotFeature
{
    All,

    ModuleManager,

    WindowManager,

    CommandManager,

    IPC,

    Prowler,

    ChainManager,
}

public static class OcelotFeatureEx
{
    private static IList<OcelotFeature> Enabled = [];

    public static void SetFeatures(IList<OcelotFeature> features)
    {
        Enabled = features;
    }

    public static bool IsEnabled(this OcelotFeature feature)
    {
        return Enabled.HasFeature(feature);
    }

    public static bool HasFeature(this IList<OcelotFeature> features, OcelotFeature feature)
    {
        return features.ContainsAny(feature, OcelotFeature.All);
    }
}
