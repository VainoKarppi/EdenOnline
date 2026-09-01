// EOEX_fnc_sendCreateMarker
// EOEX_fnc_createMarker


params ["_marker"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

diag_log format ["CREATE_MARKER: %1", _marker];

private _id = call EOEX_fnc_getId;

EOEX_var_Markers set [_id, _marker];

private _attributes = _marker get3DENAttributes "";

["CreateObject", [_id, "Marker", _attributes]] call EOEX_fnc_callExtensionAsync;
