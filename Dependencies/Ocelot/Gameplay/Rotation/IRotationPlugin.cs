using System;

namespace Ocelot.Gameplay.Rotation;

public interface IRotationPlugin : IDisposable
{
    void DisableAoe();
}
