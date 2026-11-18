// EOEX_fnc_sendCreateMarker

params ["_id", "_marker","_layerId"];

private _class = (_marker get3DENAttribute "ItemClass") select 0; // Eg. "mil_destroy", "mil_ambush" ...
private _position = (_marker get3DENAttribute "Position") select 0;


// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, "Marker", [["ItemClass", _class], ["Position", _position], ["Layer", _layerId]]], true] spawn EOEX_fnc_callExtensionAsync;
