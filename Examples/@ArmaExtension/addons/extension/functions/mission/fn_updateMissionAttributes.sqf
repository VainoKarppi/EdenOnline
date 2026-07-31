
params ["_section","_property","_value"];

private _connected = missionNamespace getVariable ["EXT_var_Connected", false];

if !(_connected) exitWith {}; // Not connected to server yet!

["SetMissionAttribute", _this] call EXT_fnc_callExtensionAsync;