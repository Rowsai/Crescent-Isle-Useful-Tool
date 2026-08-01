using System.Collections.Generic;
using CrescentIsleUsefulTool.Chains;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.CriticalEncounters;
using CrescentIsleUsefulTool.Modules.Fates;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Commands;
using Ocelot.IPC;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Commands;

[OcelotCommand]
public class TeleportCommand(Plugin plugin) : OcelotCommand
{
    protected override string Command
    {
        get => "/ciuttp";
    }

    protected override string Description
    {
        get => @"
発生中のアクティビティに最も近いエーテライトへ移動します。
 - /ciuttp pot : マジックポットを優先
 - /ciuttp ce : CEを対象にする
 - /ciuttp fate : 通常FATEを対象にする
--------------------------------
".Trim();
    }

    protected override IReadOnlyList<string> Aliases
    {
        get => ["/crescenttp"];
    }

    protected override IReadOnlyList<string> ValidArguments
    {
        get => ["pot", "ce", "fate"];
    }

    public override void Execute(string command, string arguments)
    {
        if (ZoneData.GetNearbyAethernetShards().Count <= 0)
        {
            Svc.Chat.Print("エーテライトの近くにいません。");
            return;
        }

        var lifestream = plugin.IPC.GetSubscriber<Lifestream>();
        if (!LifestreamIpc.TryIsBusy(lifestream, out var isBusy) || isBusy)
        {
            Svc.Chat.Print("Lifestreamが処理中です。");
            return;
        }

        Aethernet? shard = null;
        if (arguments.Length <= 0)
        {
            shard ??= GetPotFateAethernet();
            shard ??= GetCriticalEncounterAethernet();
            shard ??= GetFateAethernet();
        }
        else
        {
            switch (arguments)
            {
                case "fate":
                    shard = GetFateAethernet();
                    break;
                case "ce":
                    shard = GetCriticalEncounterAethernet();
                    break;
                case "pot":
                    shard = GetPotFateAethernet();
                    break;
            }
        }

        if (shard == null)
        {
            Svc.Chat.Print("移動先のエーテライトが見つかりませんでした。");
            return;
        }

        if (ZoneData.IsNearAethernetShard((Aethernet)shard))
        {
            Svc.Chat.Print("すでに最寄りのエーテライト付近にいます。");
            return;
        }

        Plugin.Chain.Submit(ChainHelper.TeleportChain((Aethernet)shard));
    }

    private Aethernet? GetFateAethernet()
    {
        var source = plugin.Modules.GetModule<FatesModule>();
        foreach (var fate in source.fates.Values)
        {
            if (fate.IsPotFate())
            {
                continue;
            }

            return fate.GetAethernet();
        }

        return null;
    }

    private Aethernet? GetPotFateAethernet()
    {
        var source = plugin.Modules.GetModule<FatesModule>();
        foreach (var fate in source.fates.Values)
        {
            if (!fate.IsPotFate())
            {
                continue;
            }

            return fate.GetAethernet();
        }

        return null;
    }

    private Aethernet? GetCriticalEncounterAethernet()
    {
        var source = plugin.Modules.GetModule<CriticalEncountersModule>();
        foreach (var encounter in source.CriticalEncounters.Values)
        {
            if (encounter.EventType >= 4 || encounter.State != DynamicEventState.Register)
            {
                continue;
            }

            var data = EventData.GetCriticalEncounter(encounter.DynamicEventId);
            return data.Aethernet ?? ZoneData.GetClosestAethernetShard(data.StartPosition ?? encounter.Position);
        }

        return null;
    }
}
