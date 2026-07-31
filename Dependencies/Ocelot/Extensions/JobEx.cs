using System.Collections.Generic;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using Lumina.Excel.Sheets;
using Ocelot.Gameplay;

namespace Ocelot.Extensions;

public static class JobEx
{
    private readonly static Dictionary<uint, ClassJob?> Data = [];

    private static ClassJob? GetData(this Job job)
    {
        var key = (uint)job;
        if (!Data.TryGetValue(key, out var value))
        {
            value = Svc.Data.GetExcelSheet<ClassJob>().GetRowOrDefault(key);
            Data.Add(key, value);
        }

        return value;
    }

    public static bool IsMelee(this Job job)
    {
        var data = job.GetData();
        if (data == null)
        {
            return true;
        }

        // 0 = crafter/gatherer, 1 = tank, 2 = melee
        return data.Value.Role <= 2;
    }

    public static float GetRange(this Job job)
    {
        return job.IsMelee() ? 3.5f : 25f;
    }

    public static bool IsTank(this Job job)
    {
        var data = job.GetData();
        if (data == null)
        {
            return false;
        }

        return data.Value.Role == 1;
    }

    public static bool HasTankStanceOn(this Job job)
    {
        return !job.IsTank() || TankHelper.Current!.HasStanceOn();
    }
}
