using System;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace EdenOnline;


public static class ObjectManager
{
    public static ConcurrentDictionary<string, ArmaObject> Objects { get; set; } = new();

    /// <summary>Adds a new object or overwrites existing with same Id.</summary>
    public static void AddObject(ArmaObject obj)
    {
        Objects[obj.Id] = obj;
    }

    /// <summary>Remove object by Id.</summary>
    public static bool RemoveObject(string id)
    {
        return Objects.TryRemove(id, out _);
    }

    /// <summary>Get object by Id.</summary>
    public static bool TryGetObject(string id, out ArmaObject? obj)
    {
        return Objects.TryGetValue(id, out obj);
    }

    /// <summary>Get a snapshot of all objects (for broadcasting to clients).</summary>
    public static List<ArmaObject> GetAllObjects()
    {
        return [.. Objects.Values];
    }

    /// <summary>Clear all objects (e.g., when mission ends).</summary>
    public static void Clear()
    {
        Objects.Clear();
    }

    /// <summary>Update object properties safely if it exists.</summary>
    public static bool UpdateObject(string id, Action<ArmaObject> updater)
    {
        if (Objects.TryGetValue(id, out var obj))
        {
            updater(obj);
            return true;
        }
        return false;
    }
}
