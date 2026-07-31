using System;
using Ocelot.Modules;
using Ocelot.ScoreBased;

namespace Ocelot.States;

public class ScoreEntranceState<T, M>(M module) : ScoreStateHandler<T, M>(module)
    where T : struct, Enum
    where M : IModule
{
    public override bool Handle()
    {
        return true;
    }

    public override float GetScore()
    {
        return -100f;
    }
}
