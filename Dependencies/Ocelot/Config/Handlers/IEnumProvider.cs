using System;

namespace Ocelot.Config.Handlers;

public interface IEnumProvider<T>
    where T : Enum
{
    bool Filter(T item);

    string GetLabel(T item);
}
