using System;
using ECommons.DalamudServices;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;

namespace Ocelot.Data;

public static class LgbHelper
{
    public static LgbFile? GetLgbFileForTerritory(TerritoryType territory, LgbType type)
    {
        var bg = territory.Bg.ExtractText();
        var path = "bg/" + bg![..(bg.IndexOf("/level/", StringComparison.Ordinal) + 1)] + $"level/{type.GetFileName()}";

        return Svc.Data.GetFile<LgbFile>(path);
    }

    public static LgbFile? GetLgbFileForTerritory(uint id, LgbType type)
    {
        if (id == 0)
        {
            return null;
        }

        var territory = Svc.Data.GetExcelSheet<TerritoryType>()?.GetRow(id);
        if (!territory.HasValue)
        {
            throw new ArgumentException($"No Territory found with id {id}");
        }

        return GetLgbFileForTerritory(territory.Value, type);
    }


    public static LgbFile? GetLgbFileForCurrentTerritory(LgbType type)
    {
        return GetLgbFileForTerritory(Svc.ClientState.TerritoryType, type);
    }
}
