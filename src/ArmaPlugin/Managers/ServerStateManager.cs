using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using static EdenOnline.Logger;
using EdenOnline;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using DynTypeNetwork;
using System.Reflection;
using System.Threading;
using static DynTypeNetwork.MethodBuilder;

namespace EdenOnline;



public static class ServerStateManager
{
    private static int initialSyncReady = 1;
    private static int initialSyncHostClientId;

    public static Dictionary<int, string> UsernameList { get; set; } = [];
    public static ClientStateManager ServerObjectManager { get; } = new ClientStateManager();
    public static MissionAttributeManager MissionAttributeManager { get; } = new MissionAttributeManager();
    public static ConcurrentDictionary<(string FromID, string ToID, string Type), ArmaSyncConnection> SyncConnections { get; } = new();
    public static ConcurrentDictionary<int, List<ArmaObject>> ObjectSyncSnapshots { get; } = new();

    /// <summary>
    /// True only after the host has uploaded and verified its initial mission
    /// snapshot. Remote clients must not synchronize against partial state.
    /// </summary>
    public static bool IsInitialSyncReady => Volatile.Read(ref initialSyncReady) == 1;

    public static void BeginInitialSync(int hostClientId = 0)
    {
        Volatile.Write(ref initialSyncHostClientId, hostClientId);
        Volatile.Write(ref initialSyncReady, 0);
    }

    public static void SetInitialSyncHost(int hostClientId)
    {
        if (hostClientId < 1) throw new ArgumentOutOfRangeException(nameof(hostClientId));
        Volatile.Write(ref initialSyncHostClientId, hostClientId);
    }

    public static bool IsInitialSyncHost(int clientId)
    {
        return clientId > 0 && clientId == Volatile.Read(ref initialSyncHostClientId);
    }

    internal static void MarkInitialSyncReady()
    {
        Volatile.Write(ref initialSyncReady, 1);
    }

    public static void Reset()
    {
        UsernameList.Clear();
        ServerObjectManager.Clear();
        MissionAttributeManager.Clear();
        SyncConnections.Clear();
        ObjectSyncSnapshots.Clear();
        Volatile.Write(ref initialSyncHostClientId, 0);
        Volatile.Write(ref initialSyncReady, 1);
    }
}
