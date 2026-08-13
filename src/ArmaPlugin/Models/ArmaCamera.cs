using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EdenOnline;


public class ArmaCamera
{
    public int Id { get; set; }
    public object[] Position { get; set; } = [0,0,0];
    public object[] Direction { get; set; } = [0,0,0];
}