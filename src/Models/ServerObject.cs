using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EdenOnline.Models;


public class ServerObject
{
    public string Id { get; set; } = "";
    public string Classname { get; set; } = "";
    public object[] Position { get; set; } = Array.Empty<object>();
    public object[] Rotation { get; set; } = Array.Empty<object>();
    public string? ParentId { get; set; }
    public string? GroupId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public double Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public ServerObject(string id, string classname, object[] position, object[] rotation, string? parentId = null, string? groupId = null, Dictionary<string, object>? metadata = null)
    {
        Id = id;
        Classname = classname;
        Position = position;
        Rotation = rotation;
        ParentId = parentId;
        GroupId = groupId;
        Metadata = metadata;
    }
}

