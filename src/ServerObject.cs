using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ArmaExtension;

public static partial class EdenOnline
{
    public class ServerObject
    {
        public string Id { get; init; }
        public string Classname { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Rotation { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        public ServerObject(string id, string type, float x, float y, float z, float rotation, Dictionary<string, object>? metadata = null)
        {
            Id = id;
            Classname = type;
            X = x;
            Y = y;
            Z = z;
            Rotation = rotation;
            Metadata = metadata;
        }
    }

    public class ServerObjectManager
    {
        private readonly ConcurrentDictionary<string, ServerObject> _objects = new();

        /// <summary>Adds a new object or overwrites existing with same Id.</summary>
        public void AddOrUpdateObject(ServerObject obj)
        {
            _objects[obj.Id] = obj;
        }

        /// <summary>Remove object by Id.</summary>
        public bool RemoveObject(string id)
        {
            return _objects.TryRemove(id, out _);
        }

        /// <summary>Get object by Id.</summary>
        public bool TryGetObject(string id, out ServerObject? obj)
        {
            return _objects.TryGetValue(id, out obj);
        }

        /// <summary>Get a snapshot of all objects (for broadcasting to clients).</summary>
        public List<ServerObject> GetAllObjects()
        {
            return new List<ServerObject>(_objects.Values);
        }

        /// <summary>Clear all objects (e.g., when mission ends).</summary>
        public void Clear()
        {
            _objects.Clear();
        }

        /// <summary>Update object properties safely if it exists.</summary>
        public bool UpdateObject(string id, Action<ServerObject> updater)
        {
            if (_objects.TryGetValue(id, out var obj))
            {
                updater(obj);
                return true;
            }
            return false;
        }
    }
}
