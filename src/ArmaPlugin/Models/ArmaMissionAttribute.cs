
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EdenOnline;

public class MissionAttribute
{
    public string? Property { get; set; }
    public string? Section { get; set; }
    public object? Value { get; set; }

    public MissionAttribute() {}
    
    public MissionAttribute(string section, string property, object? value)
    {
        Section = section;
        Property = property;
        Value = value;
    }
}