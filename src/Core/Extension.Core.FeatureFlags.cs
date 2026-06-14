using System;
using System.Runtime.InteropServices;

namespace ArmaExtension;

public static partial class Extension
{
    [Flags]
    public enum RVExtensionFeature : ulong
    {
        None = 0,
        ContextArgumentsVoidPtr = 1 << 0,
        ContextStackTrace = 1 << 1,
        ContextNoDefaultCall = 1 << 2
    }

    private static RVExtensionFeature _featureFlags =
        RVExtensionFeature.ContextArgumentsVoidPtr |
        RVExtensionFeature.ContextStackTrace |
        RVExtensionFeature.ContextNoDefaultCall;

    public static RVExtensionFeature FeatureFlags
    {
        get => _featureFlags;
        set => _featureFlags = value;
    }

    public static bool IsFeatureEnabled(RVExtensionFeature flag)
        => (_featureFlags & flag) != 0;

    public static void EnableFeature(RVExtensionFeature flag)
        => _featureFlags |= flag;

    public static void DisableFeature(RVExtensionFeature flag)
        => _featureFlags &= ~flag;

    [UnmanagedCallersOnly(EntryPoint = "RVExtensionGetFeatureFlags")]
    public static ulong RVExtensionGetFeatureFlags() => (ulong)_featureFlags;

    [DllImport("ArmaExtension_x64.dll", EntryPoint = "RVExtensionGetFeatureFlags")]
    public static extern ulong GetFeatureFlagsNative();


    public enum PredefinedFlags
    {
        ArgumentsVoidPtr,
        StackTrace,
        NoDefaultCall
    }

    /// <summary>
    /// Enable or disable a predefined flag using a bool.
    /// Returns true if the operation succeeded.
    /// </summary>
    public static bool SetFeatureFlag(PredefinedFlags flag, bool enable)
    {
        RVExtensionFeature rvFlag = flag switch
        {
            PredefinedFlags.ArgumentsVoidPtr => RVExtensionFeature.ContextArgumentsVoidPtr,
            PredefinedFlags.StackTrace => RVExtensionFeature.ContextStackTrace,
            PredefinedFlags.NoDefaultCall => RVExtensionFeature.ContextNoDefaultCall,
            _ => RVExtensionFeature.None
        };

        if (rvFlag == RVExtensionFeature.None) return false;

        if (enable)
            EnableFeature(rvFlag);
        else
            DisableFeature(rvFlag);

        return true;
    }
}