using System;

namespace Ocelot.Windows;

public class UnableToLoadWindowException : Exception
{
    public UnableToLoadWindowException(string message) : base(message)
    {
    }
}
