using System;
using System.Numerics;
using CrescentIsleUsefulTool.Enums;
using CrescentIsleUsefulTool.Ipc;
using CrescentIsleUsefulTool.Modules.Mount;
using CrescentIsleUsefulTool.Modules.Mount.Chains;
using CrescentIsleUsefulTool.Modules.Teleporter;
using CrescentIsleUsefulTool.Modules.Treasure;
using ECommons.GameHelpers;
using Ocelot.Chain;
using Ocelot.Chain.ChainEx;
using Ocelot.IPC;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Chains;

public class ChainHelper
{
    private static ChainHelper? _instance = null;

    private static ChainHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new InvalidOperationException("ChainHelper has not been initialized. Call Initialize(plugin) first.");
            }

            return _instance;
        }
    }

    private readonly Plugin Plugin;

    private static ModuleManager Modules
    {
        get => Instance.Plugin.Modules;
    }

    private static IPCManager IPC
    {
        get => Instance.Plugin.IPC;
    }

    private ChainHelper(Plugin plugin)
    {
        Plugin = plugin;
    }

    public static void Initialize(Plugin plugin)
    {
        _instance ??= new ChainHelper(plugin);
    }

    public static ReturnChain ReturnChain()
    {
        var config = new ReturnChainConfig
        {
            ApproachAetheryte = true,
        };

        return ReturnChain(config);
    }

    public static ReturnChain ReturnChain(ReturnChainConfig config)
    {
        return new ReturnChain(Modules.GetModule<TeleporterModule>(), config);
    }

    public static Func<Chain> TankyushinAtKnowledgeCrystalChain(
        bool alwaysUseDemiReturn = false,
        bool updateTreasureCount = false)
    {
        return () => Chain.Create("TankyushinAtKnowledgeCrystal")
            .Then(ReturnChain(new ReturnChainConfig
            {
                ForceReturn = true,
                AlwaysUseDemiReturn = alwaysUseDemiReturn,
                WaitForStationaryDemiReturn = true,
                ApproachAetheryte = true,
                ApplyBuffs = true,
                ForceTankyushin = true,
                UpdateTreasureCount = updateTreasureCount,
            }));
    }

    public static TeleportChain TeleportChain(Aethernet aethernet)
    {
        return new TeleportChain(
            aethernet,
            IPC.GetSubscriber<Lifestream>(),
            Modules.GetModule<TeleporterModule>()
        );
    }

    public static MountChain MountChain()
    {
        return new MountChain(Modules.GetModule<MountModule>().Config);
    }

    public static Func<Chain> PathfindToAndWait(Vector3 destination, float distance)
    {
        var vnav = IPC.GetSubscriber<VNavmesh>();
        return () => Chain.Create()
            .ConditionalThen(_ => Player.DistanceTo(destination) > distance, _ =>
                Chain.Create()
                    .Then(new PathfindAndMoveToChain(vnav, destination))
                    .WaitUntilNear(vnav, destination, distance)
                    .Then(_ => { VnavmeshIpc.TryStop(vnav); })
            );
    }

    public static Func<Chain> MoveToAndWait(Vector3 destination, float distance)
    {
        var vnav = IPC.GetSubscriber<VNavmesh>();
        return () => Chain.Create()
            .ConditionalThen(_ => Player.DistanceTo(destination) > distance, _ =>
                Chain.Create()
                    .Then(_ => VnavmeshIpc.TryFollowPath(vnav, [destination], false))
                    .WaitUntilNear(vnav, destination, distance)
                    .Then(_ => { VnavmeshIpc.TryStop(vnav); })
            );
    }

    public static TreasureSightChain TreasureSightChain(bool force = false)
    {
        return new TreasureSightChain(Modules.GetModule<TreasureModule>(), force);
    }
}
