using System;

namespace Ocelot.Modules;

public class UnableToLoadModuleException : Exception
{
    public UnableToLoadModuleException(string message) : base(message)
    {
    }
}
