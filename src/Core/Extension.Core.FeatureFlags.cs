

using System;
using System.Runtime.InteropServices;

namespace ArmaExtension;

public static partial class Extension {
    [Flags]
    public enum RVExtensionFeature : ulong {
        None = 0,
        ContextArgumentsVoidPtr = 1 << 0,
        ContextStackTrace = 1 << 1,
        ContextNoDefaultCall = 1 << 2
    }

    private static RVExtensionFeature _featureFlags = RVExtensionFeature.ContextArgumentsVoidPtr | RVExtensionFeature.ContextStackTrace | RVExtensionFeature.ContextNoDefaultCall;

    /// <summary>
    /// Get or set the current feature flags for the extension.
    /// Changing these at runtime affects how Arma sees your extension.
    /// </summary>
    public static RVExtensionFeature FeatureFlags {
        get => _featureFlags;
        set => _featureFlags = value;
    }

    /// <summary>
    /// Helper methods to enable or disable individual flags dynamically
    /// </summary>
    public static void EnableFeature(RVExtensionFeature flag) => _featureFlags |= flag;
    public static void DisableFeature(RVExtensionFeature flag) => _featureFlags &= ~flag;
    public static bool IsFeatureEnabled(RVExtensionFeature flag) => (_featureFlags & flag) != 0;


    // TODO C/C++ WARPPER IS REQUIRED IN ORDER TO SET THE FLAG
    // TODO https://github.com/dotnet/runtime/issues/93936
    
    /// <summary>
    /// Called by Arma to query the features your extension supports
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "RVExtensionFeatureFlags")]
    public static ulong RVExtensionFeatureFlags() => (ulong)_featureFlags;
}