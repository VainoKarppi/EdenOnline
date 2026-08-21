// EOEX_fnc_updateMissionAttributes

params ["_section","_property","_value"];

private _connected = missionNamespace getVariable ["EOEX_var_Connected", false];

if !(_connected) exitWith {}; // Not connected to server yet!

["SetMissionAttribute", _this] call EOEX_fnc_callExtensionAsync;
