using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EdenOnline.Models;


/*  ["OBJECTID",[
        ["ItemClass","B_Truck_01_mover_F"]
        ["Name",""]
        ["Init",""]
        ["Pylons",""]
        ["Position",[3106.45,1646.37,0]]
        ["Rotation",[328.749,0,0]]
        ["Size3",[0,0,0]]
        ["IsRectangle",false]
        ["PlacementRadius",0]
        ["ControlSP",false]
        ["ControlMP",false]
        ["Description",""]
        ["Lock",1]
        ["Skill",0.5]
        ["Health",1]
        ["Fuel",1]
        ["Ammo",1]
        ["Rank","PRIVATE"]
        ["UnitPos",3]
        ["DynamicSimulation",false]
        ["AddToDynSimGrid",true]
        ["EnableSimulation",true]
        ["ObjectIsSimple",false]
        ["IsLocalOnly",false]
        ["allowDamage",true]
        ["DoorStates",[0,0,0]]
        ["EnableRevive",false],
        ["hideObject",false]
        ["enableStamina",true],
        ["NameSound",""],
        ["speaker",""],
        ["pitch",-1],
        ["unitName","Archie Moore"],
        ["unitInsignia",""],
        ["face",""],
        ["Presence",1],
        ["PresenceCondition","true"],
        ["ammoBox","[[[[],[]],[[],[]],[[""FirstAidKit""],[13]],[[],[]]],false]"],
        ["VehicleCustomization",[[],[]]],
        ["ReportRemoteTargets",false],
        ["ReceiveRemoteTargets",false],
        ["ReportOwnPosition",false],
        ["RadarUsageAI",0]
    ]]
*/
public class ArmaObject
{
    public string Id { get; set; } = "";
    public Dictionary<string, object?>? Attributes { get; set; }
    public double Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public ArmaObject() { }
    
    public ArmaObject(string id, Dictionary<string, object?>? attributes = null)
    {
        Id = id;
        Attributes = attributes;
    }
}


public class ArmaCamera
{
    public int Id { get; set; }
    public object[]? Position { get; set; }
    public object[]? Direction { get; set; }
    public object[]? Up { get; set; }
}
