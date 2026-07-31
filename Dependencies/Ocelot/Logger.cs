using System;
using ECommons.DalamudServices;

namespace Ocelot;

internal sealed class Logger
{
    public static void Info(string messageTemplate, params object[] values)
    {
        Svc.Log.Info($"[Ocelot] {messageTemplate}", values);
    }

    public static void Info(Exception? exception, string messageTemplate, params object[] values)
    {
        Svc.Log.Info(exception, $"[Ocelot] {messageTemplate}", values);
    }

    public static void Fatal(string messageTemplate, params object[] values)
    {
        Svc.Log.Fatal($"[Ocelot] {messageTemplate}", values);
    }

    public static void Fatal(Exception? exception, string messageTemplate, params object[] values)
    {
        Svc.Log.Fatal(exception, $"[Ocelot] {messageTemplate}", values);
    }

    public static void Error(string messageTemplate, params object[] values)
    {
        Svc.Log.Error($"[Ocelot] {messageTemplate}", values);
    }

    public static void Error(Exception? exception, string messageTemplate, params object[] values)
    {
        Svc.Log.Error(exception, $"[Ocelot] {messageTemplate}", values);
    }

    public static void Warning(string messageTemplate, params object[] values)
    {
        Svc.Log.Warning($"[Ocelot] {messageTemplate}", values);
    }

    public static void Warning(Exception? exception, string messageTemplate, params object[] values)
    {
        Svc.Log.Warning(exception, $"[Ocelot] {messageTemplate}", values);
    }

    public static void Debug(string messageTemplate, params object[] values)
    {
        Svc.Log.Debug($"[Ocelot] {messageTemplate}", values);
    }

    public static void Debug(Exception? exception, string messageTemplate, params object[] values)
    {
        Svc.Log.Debug(exception, $"[Ocelot] {messageTemplate}", values);
    }

    public static void Verbose(string messageTemplate, params object[] values)
    {
        Svc.Log.Verbose($"[Ocelot] {messageTemplate}", values);
    }

    public static void Verbose(Exception? exception, string messageTemplate, params object[] values)
    {
        Svc.Log.Verbose(exception, $"[Ocelot] {messageTemplate}", values);
    }

    public static void Warn(string p0)
    {
        throw new NotImplementedException();
    }
}
