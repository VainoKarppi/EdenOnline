using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static ArmaExtension.Logger;

namespace ArmaExtension;

[AttributeUsage(AttributeTargets.Class)]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
internal sealed class ArmaExtensionPluginAttribute : Attribute
{
}

internal static partial class PluginLoader
{
    private static bool _initialized;

    internal static bool InitializePlugins()
    {
        if (_initialized) return false;

        Log("Initializing plugins...");

        var assembly = Assembly.GetExecutingAssembly();

        foreach (var type in assembly.GetTypes())
        {
            if (!IsValidPlugin(type))
                continue;

#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
            InvokeMain(type);
#pragma warning restore IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
        }

        _initialized = true;
        return true;
    }

    private static bool IsValidPlugin(Type type)
    {
        return type.IsClass
            && type.IsAbstract
            && type.IsSealed
            && type.IsDefined(typeof(ArmaExtensionPluginAttribute), inherit: false);
    }

    /// <summary>
    /// Reflection isolated into annotated method so the trimmer can track it.
    /// </summary>
    private static void InvokeMain(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        Type pluginType)
    {
        var mainMethod = pluginType.GetMethod(
            "Main",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        if (mainMethod != null && mainMethod.GetParameters().Length == 0)
        {
            Debug($"Invoking {pluginType.FullName}.Main()");
            mainMethod.Invoke(null, null);
        }
    }
}