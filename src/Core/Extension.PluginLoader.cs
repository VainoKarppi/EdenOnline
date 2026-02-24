using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using ArmaExtension;
using static ArmaExtension.Logger;

// [assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MyExtension))]
// Moved DynamicDependency attribute to a valid location below
namespace ArmaExtension;

[AttributeUsage(AttributeTargets.Class)]
public class ArmaExtensionPluginAttribute : Attribute { }


public static partial class Extension
{
    private static bool _initialized = false;

    private static bool InitializePlugins()
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
                Log($"Invoking {type.FullName}.Main()");
                mainMethod.Invoke(null, null);
            }
        }

        _initialized = true;
        return true;
    }
}
