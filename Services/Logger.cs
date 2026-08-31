using System;
using Serilog;

namespace GBMS.Services;

public static class Logger
{
    public static void Trace(string messageTemplate, params object[] propertyValues)
    {
        Log.Verbose(messageTemplate, propertyValues);
    }

    public static void Debug(string messageTemplate, params object[] propertyValues)
    {
        Log.Debug(messageTemplate, propertyValues);
    }

    public static void Information(string messageTemplate, params object[] propertyValues)
    {
        Log.Information(messageTemplate, propertyValues);
    }

    public static void Warning(string messageTemplate, params object[] propertyValues)
    {
        Log.Warning(messageTemplate, propertyValues);
    }

    public static void Error(string messageTemplate, params object[] propertyValues)
    {
        Log.Error(messageTemplate, propertyValues);
    }

    public static void Error(
        Exception exception,
        string messageTemplate,
        params object[] propertyValues)
    {
        Log.Error(exception, messageTemplate, propertyValues);
    }

    public static void Critical(string messageTemplate, params object[] propertyValues)
    {
        Log.Fatal(messageTemplate, propertyValues);
    }

    public static void Critical(
        Exception exception,
        string messageTemplate,
        params object[] propertyValues)
    {
        Log.Fatal(exception, messageTemplate, propertyValues);
    }
}