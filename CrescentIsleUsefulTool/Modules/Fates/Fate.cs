using System;
using System.Numerics;
using CrescentIsleUsefulTool.Data;
using CrescentIsleUsefulTool.Enums;
using Dalamud.Game;
using ECommons.DalamudServices;

namespace CrescentIsleUsefulTool.Modules.Fates;

/// <summary>
/// A managed snapshot of an active FATE. Dalamud's IFate points into the
/// game's live FATE table and must not be retained after the framework update
/// in which it was obtained. Keeping that pointer after a FATE despawns can
/// turn an innocent UI name read into an uncatchable access violation.
/// </summary>
public class Fate
{
    public readonly EventData Data;

    public readonly DateTime SpawnedAt;

    public uint Id { get; }

    public string Name { get; }

    public float Radius { get; private set; }

    public Vector3 StartPosition { get; private set; }

    public readonly EventProgress Progress = new();

    public byte CurrentProgress { get; private set; }

    public Fate(uint id, byte currentProgress, float runtimeRadius, Vector3 runtimePosition)
    {
        Id = id;
        Data = EventData.GetFate(id);
        SpawnedAt = DateTime.Now;
        Name = ResolveName(id, Data.InternalName);
        Radius = Data.Radius ?? runtimeRadius;
        StartPosition = Data.StartPosition ?? runtimePosition;
        Refresh(currentProgress, runtimeRadius, runtimePosition);
    }

    public void Refresh(byte currentProgress, float runtimeRadius, Vector3 runtimePosition)
    {
        CurrentProgress = currentProgress;
        if (Data.Radius == null && runtimeRadius > 0f)
        {
            Radius = runtimeRadius;
        }

        if (Data.StartPosition == null && runtimePosition != Vector3.Zero)
        {
            StartPosition = runtimePosition;
        }

        if (CurrentProgress <= 0)
        {
            return;
        }

        if (Progress.Count == 0 || Progress.Latest != CurrentProgress)
        {
            Progress.Add(CurrentProgress);
        }
    }

    public bool IsPotFate()
    {
        return Data.IsPot || Data.Note == MonsterNote.PersistentPots;
    }

    public Aethernet GetAethernet()
    {
        return Data.Aethernet ?? ZoneData.GetClosestAethernetShard(StartPosition);
    }

    private static string ResolveName(uint id, string fallback)
    {
        var sheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Fate>(ClientLanguage.Japanese);
        if (sheet.TryGetRow(id, out var row))
        {
            var name = row.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? $"FATE {id}" : fallback;
    }
}
