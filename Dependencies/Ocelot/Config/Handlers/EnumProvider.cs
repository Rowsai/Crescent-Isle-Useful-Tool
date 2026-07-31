using System;

namespace Ocelot.Config.Handlers;

public abstract class EnumProvider<T> : IEnumProvider<T>
    where T : Enum
{
    public virtual bool Filter(T item)
    {
        return true;
    }

    public virtual string GetLabel(T item)
    {
        return item.ToString();
    }
}
