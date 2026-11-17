

params ["_id", "_comment", "_layerId"];

private _class = "loc_talk"; // FIXED
private _position = (_comment get3DENAttribute "Position") select 0;


// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, "Comment", [["ItemClass", _class], ["Position", _position], ["Layer", _layerId]]], true] spawn EOEX_fnc_callExtensionAsync;

