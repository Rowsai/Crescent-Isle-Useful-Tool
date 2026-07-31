using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using BOCCHI.Enums;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;

namespace BOCCHI.Data;

public static class ZoneData
{
    public const uint SOUTHHORN = 1252;

    public const uint NORTHHORN = 1346;

    // This can and should be filled using layout files or excel data
    public readonly static Dictionary<uint, Vector3> Aetherytes = new()
    {
        { SOUTHHORN, new Vector3(830.75f, 72.98f, -695.98f) },
        { NORTHHORN, new Vector3(880.0015f, 259.7396f, 880.0587f) },
    };

    public readonly static Dictionary<uint, Vector3> StartingLocations = new()
    {
        { SOUTHHORN, new Vector3(850.33f, 72.99f, -704.07f) },
        { NORTHHORN, new Vector3(888.4536f, 258.5f, 882.024f) },
    };

    private readonly static Dictionary<uint, Aethernet[]> Aethernets = new()
    {
        {
            SOUTHHORN,
            [
                Aethernet.BaseCamp,
                Aethernet.TheWanderersHaven,
                Aethernet.CrystallizedCaverns,
                Aethernet.Eldergrowth,
                Aethernet.Stonemarsh,
            ]
        },
        {
            NORTHHORN,
            [
                Aethernet.NorthBaseCamp,
                Aethernet.SunkenTempleFront,
                Aethernet.FloatingRuins,
                Aethernet.RuinedStreetsFront,
                Aethernet.WillOWispVillage,
                Aethernet.KarnakCitadel,
            ]
        },
    };

    // Zone functions
    public static bool IsInSouthHorn()
    {
        return Svc.ClientState.TerritoryType == SOUTHHORN;
    }

    public static bool IsInNorthHorn()
    {
        return Svc.ClientState.TerritoryType == NORTHHORN;
    }

    public static bool IsOccultCrescentTerritory(uint territoryId)
    {
        return territoryId is SOUTHHORN or NORTHHORN;
    }

    public static bool IsInOccultCrescent()
    {
        return Svc.Objects.LocalPlayer != null && IsOccultCrescentTerritory(Svc.ClientState.TerritoryType);
    }

    // Tower functions
    private static bool IsInForkedTowerBlood()
    {
        var player = Svc.Objects.LocalPlayer;
        if (player == null)
        {
            return false;
        }

        return player.StatusList.HasAny(
            PlayerStatus.DutiesAsAssigned,
            PlayerStatus.ResurrectionDenied,
            PlayerStatus.ResurrectionRestricted
        ) && IsOccultCrescentTerritory(Svc.ClientState.TerritoryType);
    }

    public static bool IsInForkedTower()
    {
        return IsInForkedTowerBlood();
    }

    private static string GetCurrentZoneName()
    {
        if (IsInSouthHorn())
        {
            return "South Horn";
        }

        if (IsInNorthHorn())
        {
            return "North Horn";
        }

        throw new Exception("Unknown Zone");
    }

    public static string GetCurrentZoneDataDirectory()
    {
        var directory = Path.Join(Svc.PluginInterface.AssemblyLocation.DirectoryName, "Data", GetCurrentZoneName().Replace(" ", ""));
        Directory.CreateDirectory(directory);

        return directory;
    }

    public static Aethernet GetClosestAethernetShard(Vector3 position)
    {
        return AethernetData.All().OrderBy((data) => Vector3.Distance(position, data.Position)).First()!.Aethernet;
    }

    public static IReadOnlyList<Aethernet> GetCurrentAethernets()
    {
        return Aethernets.TryGetValue(Svc.ClientState.TerritoryType, out var aethernet) ? aethernet : [];
    }

    public static IList<IGameObject> GetNearbyAethernetShards(float range = 4.3f)
    {
        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;

        return Svc.Objects
            .Where(o => o.ObjectKind == ObjectKind.EventObj)
            .Where(o => AethernetData.All().Select((datum) => datum.BaseId).Contains(o.BaseId))
            .Where(o => Vector3.Distance(o.Position, playerPos) <= range)
            .ToList();
    }

    public static bool IsNearAethernetShard(Aethernet aethernet, float range = 4.3f)
    {
        return GetNearbyAethernetShards(range).Any(o => o.BaseId == aethernet.GetData().BaseId);
    }

    public static IList<IGameObject> GetNearbyKnowledgeCrystal(float range = 4.5f)
    {
        var playerPos = Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero;

        return Svc.Objects
            .Where(o => o.ObjectKind == ObjectKind.EventObj)
            .Where(o => o.BaseId == (uint)OccultObjectType.KnowledgeCrystal)
            .Where(o => Vector3.Distance(o.Position, playerPos) <= range)
            .ToList();
    }

    public static bool IsNearKnowledgeCrystal(float range = 4.5f)
    {
        return GetNearbyKnowledgeCrystal(range).Any();
    }
}
