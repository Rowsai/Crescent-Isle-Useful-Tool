using System.Numerics;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using XIVTreasure = Lumina.Excel.Sheets.Treasure;

namespace CrescentIsleUsefulTool.Modules.Treasure;

/// <summary>
/// Managed treasure snapshot. The source IGameObject is deliberately not
/// retained because treasure objects are destroyed as soon as they open.
/// </summary>
public sealed class Treasure
{
    public ulong Id { get; }

    public uint BaseId { get; }

    public Vector3 Position { get; }

    private TreasureType Type { get; }

    public Treasure(IGameObject obj)
    {
        Id = obj.EntityId;
        BaseId = obj.BaseId;
        Position = obj.Position;
        Type = ResolveType(BaseId);
    }

    public bool IsValid()
    {
        var current = GameObjectInteraction.Resolve(Id);
        return current is { IsDead: false, IsTargetable: true } && current.BaseId == BaseId;
    }

    public Vector3 GetPosition() => Position;

    public TreasureType GetTreasureType() => Type;

    public Vector4 GetColor()
    {
        return Type switch
        {
            TreasureType.Bronze => TreasureModule.Bronze,
            TreasureType.Silver => TreasureModule.Silver,
            _ => TreasureModule.Unknown,
        };
    }

    public string GetName()
    {
        return Type switch
        {
            TreasureType.Bronze => "青銅の宝箱",
            TreasureType.Silver => "白銀の宝箱",
            _ => "種類不明の宝箱",
        };
    }

    private static TreasureType ResolveType(uint baseId)
    {
        if (!Svc.Data.GetExcelSheet<XIVTreasure>().TryGetRow(baseId, out var data))
        {
            return TreasureType.Unknown;
        }

        return data.SGB.RowId switch
        {
            TreasureData.SilverSgbId => TreasureType.Silver,
            TreasureData.BronzeSgbId => TreasureType.Bronze,
            _ => TreasureType.Unknown,
        };
    }
}
