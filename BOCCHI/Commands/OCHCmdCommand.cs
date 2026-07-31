using System.Collections.Generic;
using BOCCHI.Modules.CriticalEncounters;
using BOCCHI.Modules.Fates;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Ocelot.Commands;
using Ocelot.Modules;

namespace BOCCHI.Commands;

[OcelotCommand]
public class OCHCmdCommand(Plugin plugin) : OcelotCommand
{
    protected override string Command
    {
        get => "/ciutcmd";
    }

    protected override string Description
    {
        get => @"
アクティビティの位置へフラッグを設定します。既存のフラッグは解除されます。
 - /ciutcmd flag-active-ce : 受付中のCEへフラッグを設定
 - /ciutcmd flag-active-fate : 発生中のFATEへフラッグを設定
 - /ciutcmd flag-active-non-pot-fate : マジックポット以外のFATEへフラッグを設定
--------------------------------
".Trim();
    }

    protected override IReadOnlyList<string> Aliases
    {
        get => ["/crescentcmd"];
    }

    protected override IReadOnlyList<string> ValidArguments
    {
        get => ["flag-active-ce", "flag-active-fate", "flag-active-non-pot-fate"];
    }

    public override unsafe void Execute(string command, string arguments)
    {
        var map = AgentMap.Instance();
        map->FlagMarkerCount = 0;

        switch (arguments)
        {
            case "flag-active-ce": FlagActiveCe(map); break;
            case "flag-active-fate": FlagActiveFate(map, false); break;
            case "flag-active-non-pot-fate": FlagActiveFate(map, true); break;
        }
    }

    private unsafe void FlagActiveCe(AgentMap* map)
    {
        if (!plugin.Modules.TryGetModule<CriticalEncountersModule>(out var source) || source == null)
        {
            return;
        }

        foreach (var encounter in source.CriticalEncounters.Values)
        {
            if (encounter.EventType >= 4 || encounter.State != DynamicEventState.Register)
            {
                continue;
            }

            map->SetFlagMapMarker(Svc.ClientState.TerritoryType, Svc.ClientState.MapId, encounter.MapMarker.Position);
            return;
        }
    }

    private unsafe void FlagActiveFate(AgentMap* map, bool ignorePots)
    {
        if (!plugin.Modules.TryGetModule<FatesModule>(out var source) || source == null)
        {
            return;
        }

        foreach (var fate in source.fates.Values)
        {
            if (ignorePots && fate.IsPotFate())
            {
                continue;
            }

            map->SetFlagMapMarker(Svc.ClientState.TerritoryType, Svc.ClientState.MapId, fate.StartPosition);
            return;
        }
    }
}
