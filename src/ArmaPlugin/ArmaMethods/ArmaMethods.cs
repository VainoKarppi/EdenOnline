using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DynTypeNetwork;
using static EdenOnline.Logger;

namespace EdenOnline;

/// <summary>
/// Methods exposed to Arma 3 via callExtension.
/// This is the primary API surface that Arma scripts interact with.
/// </summary>
public static partial class ArmaMethods
{
    /// <summary>
    /// Returns the current extension version string.
    /// </summary>
    public static string Version() => Extension.Version;

    /// <summary>
    /// Computes a SHA-256 hash of the given object for verification purposes.
    /// </summary>
    public static string GetHash(object item) => HashUtils.GetHash(item);

    public static async Task StartApiServerAsync()
    {
        await ArmaApiServer.StartAsync();
    }

    public static void StopApiServer()
    {
        ArmaApiServer.Stop();
    }
}
