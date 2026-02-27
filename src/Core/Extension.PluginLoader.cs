using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using static ArmaExtension.Logger;
using static ArmaExtension.Enums;
using static ArmaExtension.MethodSystem;
using static ArmaExtension.Events;

// [assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MyExtension))]
// Moved DynamicDependency attribute to a valid location below
namespace ArmaExtension;

[AttributeUsage(AttributeTargets.Class)]
internal class ArmaExtensionPluginAttribute : Attribute { }


internal static partial class PluginLoader
{
    private static bool _initialized = false;

    internal static bool InitializePlugins()
    {
        if (_initialized) return false;

        Log($"Initializing plugins...");

        var assembly = Assembly.GetExecutingAssembly();

        foreach (var type in assembly.GetTypes())
        {
            // Only static classes (abstract + sealed)
            if (!type.IsClass || !type.IsAbstract || !type.IsSealed) continue;

#pragma warning disable IL2075
            var mainMethod = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore IL2075

            if (mainMethod != null && mainMethod.GetParameters().Length == 0)
            {
                Debug($"Invoking {type.FullName}.Main()");
                mainMethod.Invoke(null, null);
            }
        }

        _initialized = true;
        return true;
    }
}
