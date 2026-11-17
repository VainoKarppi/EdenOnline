// EOEX_fnc_sendCreateObject

// This file handles types Object, Trigger and Logic
// ItemClass, might not exist, for all objects, but we force it in the attributes

// [["ItemClass","B_AFV_Wheeled_01_cannon_F"],["Name",""],["Init","asd"],["Pylons",""],["Position",[5559.25,1769.27,0]],["Rotation",[-0,0,0]],["Size3",[0,0,0]],["IsRectangle",false],["PlacementRadius",0],["ControlSP",false],["ControlMP",false],["Description",""],["Lock",1],["Skill",0.5],["Health",1],["Fuel",1],["Ammo",1],["Rank","PRIVATE"],["UnitPos",3],["DynamicSimulation",false],["AddToDynSimGrid",true],["EnableSimulation",true],["ObjectIsSimple",false],["IsLocalOnly",false],["allowDamage",true],["DoorStates",[0,0,0]],["EnableRevive",false],["hideObject",false],["enableStamina",true],["NameSound",""],["speaker",""],["pitch",-1],["unitName","Error: No unit"],["unitInsignia",""],["face",""],["Presence",1],["PresenceCondition","true"],["ammoBox","[[[[""arifle_MXC_F""],[1]],[[""SmokeShell"",""SmokeShellBlue"",""30Rnd_65x39_caseless_mag""],[2,2,4]],[[""FirstAidKit""],[10]],[[],[]]],false]"],["VehicleCustomization",[[],[]]],["ReportRemoteTargets",false],["ReceiveRemoteTargets",false],["ReportOwnPosition",false],["RadarUsageAI",0]]
params ["_id", "_object", "_type", "_layerId"];

private _position = (_object get3DENAttribute "Position") select 0;

private _isEmpty = call {
    _object isKindOf "Man" || {count crew _object == 0}
};

// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, _type, [["ItemClass", typeOf _object], ["Position", _position], ["Layer", _layerId], ["EOEX_IS_EMPTY", _isEmpty]]], true] call EOEX_fnc_callExtensionAsync;
