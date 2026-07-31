using System;
using CrescentIsleUsefulTool.Data;

namespace CrescentIsleUsefulTool.Modules.MobFarmer;

public interface IRotationPlugin : IDisposable
{
    public void PhantomJobOn(Job? job = null);

    public void PhantomJobOff(Job? job = null);
}
