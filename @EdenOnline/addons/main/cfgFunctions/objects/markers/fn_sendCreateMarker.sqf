// EOEX_fnc_sendCreateMarker

params ["_id", "_marker"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

EOEX_var_Markers set [_id, _marker];

private _attributes = _marker get3DENAttributes "";

["CreateObject", [_id, "Marker", _attributes]] spawn EOEX_fnc_callExtensionAsync;
