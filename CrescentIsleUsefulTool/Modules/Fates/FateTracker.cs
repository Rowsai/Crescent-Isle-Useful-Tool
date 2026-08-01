using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ECommons.DalamudServices;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Fates;

public class FateTracker
{
    public readonly Dictionary<uint, Fate> Fates = [];

    public event Action<Fate>? OnFateSpawned;

    public event Action<Fate>? OnFateDespawned;


    public void Update(UpdateContext context)
    {
        // Copy only scalar values while IFate belongs to the current framework
        // update. Never store the live IFate wrapper in a long-lived object.
        var currentFates = new Dictionary<uint, FateSnapshot>();
        foreach (var current in Svc.Fates)
        {
            var id = (uint)current.FateId;
            if (id == 0)
            {
                continue;
            }

            currentFates[id] = new FateSnapshot(current.Progress, current.Radius, current.Position);
        }

        foreach (var (id, snapshot) in currentFates)
        {
            if (Fates.TryGetValue(id, out var existing))
            {
                existing.Refresh(snapshot.Progress, snapshot.Radius, snapshot.Position);
                continue;
            }

            var fate = new Fate(id, snapshot.Progress, snapshot.Radius, snapshot.Position);
            OnFateSpawned?.Invoke(fate);
            Fates[id] = fate;
        }

        var despawned = Fates.Keys.Except(currentFates.Keys).ToList();
        foreach (var id in despawned)
        {
            OnFateDespawned?.Invoke(Fates[id]);
            Fates.Remove(id);
        }

    }

    private readonly record struct FateSnapshot(byte Progress, float Radius, Vector3 Position);
}
