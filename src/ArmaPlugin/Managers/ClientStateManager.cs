using System;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace EdenOnline;


public class ClientStateManager
{
    private readonly object objectsGate = new();

    /// <summary>
    /// Tracks connected client IDs and their usernames on the client side.
    /// </summary>
    public static ConcurrentDictionary<int, string> UsernameList { get; set; } = [];
    public static ObjectDragSessionManager ObjectDragSessions { get; } = new();

    public ConcurrentDictionary<string, ArmaObject> Objects { get; set; } = new();

    /// <summary>Adds a new object or merges incoming attributes into an existing object while preserving the other attributes.</summary>
    public void AddOrUpdateObject(ArmaObject obj) {
        if (obj is null) return;

        lock (objectsGate)
        {
            AddOrUpdateObjectLocked(obj);
        }
    }

    /// <summary>
    /// Atomically applies an object revision unless a newer revision is already
    /// stored. Equal revisions are accepted so END retries remain idempotent.
    /// </summary>
    public bool AddOrUpdateObjectIfRevisionCurrent(ArmaObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        lock (objectsGate)
        {
            if (!Objects.TryGetValue(obj.Id, out ArmaObject? existing)
                || existing.Timestamp > obj.Timestamp)
                return false;

            AddOrUpdateObjectLocked(obj);
            return true;
        }
    }

    private void AddOrUpdateObjectLocked(ArmaObject obj)
    {
        if (Objects.TryGetValue(obj.Id, out var existing) && existing is not null) {
            foreach (var kvp in obj.Attributes) {
                existing.Attributes[kvp.Key] = kvp.Value;
            }

            if (obj.Timestamp != 0) existing.Timestamp = obj.Timestamp;

            Objects[obj.Id] = existing;
            return;
        }

        Objects[obj.Id] = obj;
    }

    /// <summary>Remove object by Id.</summary>
    public bool RemoveObject(string id)
    {
        lock (objectsGate)
        {
            return Objects.TryRemove(id, out _);
        }
    }

    /// <summary>Get object by Id.</summary>
    public bool TryGetObject(string id, out ArmaObject? obj)
    {
        lock (objectsGate)
        {
            return Objects.TryGetValue(id, out obj);
        }
    }

    /// <summary>Get a snapshot of all objects (for broadcasting to clients).</summary>
    public List<ArmaObject> GetAllObjects()
    {
        lock (objectsGate)
        {
            return [.. Objects.Values];
        }
    }

    /// <summary>Clear all objects (e.g., when mission ends).</summary>
    public void Clear()
    {
        lock (objectsGate)
        {
            Objects.Clear();
        }
    }

    /// <summary>Update object properties safely if it exists.</summary>
    public bool UpdateObject(string id, ArmaObject armaObject)
    {
        lock (objectsGate)
        {
            if (Objects.ContainsKey(id))
            {
                Objects[id] = armaObject;
                return true;
            }
            return false;
        }
    }
}


public class MissionAttributeManager
{
    // [["Property", "Section"], Value] - Value can be anything
    public ConcurrentDictionary<string[], object?> Attributes { get; set; } = new();

    public void SetAttribute(string[] data, object? value)
    {
        Attributes[data] = value;
    }

    public bool TryGetAttribute(string[] data, out object? value)
    {
        return Attributes.TryGetValue(data, out value);
    }

    public Dictionary<string[], object?> GetAllAttributes()
    {
        return new Dictionary<string[], object?>(Attributes);
    }

    public void Clear()
    {
        Attributes.Clear();
    }
}
