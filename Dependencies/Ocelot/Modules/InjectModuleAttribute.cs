using System;

namespace Ocelot.Modules;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class InjectModuleAttribute : Attribute;
