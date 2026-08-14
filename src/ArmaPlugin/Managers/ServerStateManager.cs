using System;
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
using static DynTypeNetwork.MethodBuilder;

namespace EdenOnline;



public static class ServerStateManager
{
    public static Dictionary<int, string> UsernameList { get; set; } = [];
    public static ClientStateManager ServerObjectManager { get; } = new ClientStateManager();
    public static MissionAttributeManager MissionAttributeManager { get; } = new MissionAttributeManager();
    public static readonly List<ArmaSyncConnection> SyncConnections = [];

    public static void Reset()
    {
        UsernameList.Clear();
        ServerObjectManager.Clear();
        MissionAttributeManager.Clear();
        SyncConnections.Clear();
    }
}