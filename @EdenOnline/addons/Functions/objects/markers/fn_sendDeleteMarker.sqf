// EOEX_fnc_sendDeleteMarker
// EOEX_fnc_deleteMarker


params ["_marker"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

private _id = call EOEX_fnc_getId;
if !(_id in EOEX_var_Markers) exitWith {}; // not found from map

diag_log format ["DELETE_MARKER: %1, ID: %2", _marker, _id];

["RemoveObject", [_id]] spawn EOEX_fnc_callExtensionAsync;

EOEX_var_Markers deleteAt _id;

deleteMarker _marker;
