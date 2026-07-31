using System;

namespace Ocelot.States;

[AttributeUsage(AttributeTargets.Class)]
public class StateAttribute<T>(T State) : Attribute
    where T : struct, Enum
{
    public readonly T State = State;
}
