// EOEX_fnc_sendUpdateMarker


params ["_marker"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

private _id = _marker call EOEX_fnc_getId;
if !(_id in EOEX_var_Markers) exitWith {}; // not found from map

diag_log format ["UPDATE_MARKER: %1, ID: %2", _marker, _id];

["UpdateMarker", [_id]] spawn EOEX_fnc_callExtensionAsync;
