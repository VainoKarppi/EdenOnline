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
    public static bool UpdateObject(string id, ArmaObject armaObject)
    {
        if (Objects.ContainsKey(id))
        {
            Objects[id] = armaObject;
            return true;
        }
        return false;
    }
}


public static class MissionAttributeManager
{
    // [["Property", "Section"], Value] - Value can be anything
    public static ConcurrentDictionary<string[], object?> Attributes { get; set; } = new();

    public static void SetAttribute(string[] data, object? value)
    {
        Attributes[data] = value;
    }

    public static bool TryGetAttribute(string[] data, out object? value)
    {
        return Attributes.TryGetValue(data, out value);
    }

    public static Dictionary<string[], object?> GetAllAttributes()
    {
        return new Dictionary<string[], object?>(Attributes);
    }

    public static void Clear()
    {
        Attributes.Clear();
    }
}