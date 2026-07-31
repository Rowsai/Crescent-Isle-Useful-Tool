using System;

namespace Ocelot.IPC;

public class UnableToLoadIpcSubscriberException(string message) : Exception(message);
