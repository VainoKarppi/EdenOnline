using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using static ArmaExtension.Logger;

namespace ArmaExtension;

public static partial class Extension {
    public readonly static string AssemblyDirectory = GetAssemblyLocation();
    public readonly static string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()!;
    public readonly static string ExtensionName = GetAssemblyName();

    [RequiresAssemblyFiles()]
    internal static string GetAssemblyLocation() {
        string? dir = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(dir)) dir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(dir)) dir = Assembly.GetAssembly(typeof(Extension))?.Location;
        if (string.IsNullOrEmpty(dir)) dir = typeof(Extension).Assembly.Location;

        if (string.IsNullOrEmpty(dir)) dir = AppContext.BaseDirectory;

        if (string.IsNullOrEmpty(dir)) dir = Assembly.GetCallingAssembly().Location;


        // Fallback to Arma 3 Directory
        if (string.IsNullOrEmpty(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
        if (string.IsNullOrEmpty(dir)) throw new DirectoryNotFoundException("Unable to locate Assembly start Directory!");
        return dir;
    }

    [RequiresAssemblyFiles()]
    private static string GetAssemblyName() {
        string name = Assembly.GetExecutingAssembly().GetName().Name!;
        if (string.IsNullOrEmpty(name)) throw new DirectoryNotFoundException("Unable to locate Assembly Name!");
        return name.EndsWith("_x64") ? name[..^4] : name;
    }
}