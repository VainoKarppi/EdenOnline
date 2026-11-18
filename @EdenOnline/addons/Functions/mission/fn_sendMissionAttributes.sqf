// EOEX_fnc_sendMissionAttributes
// EOEX_fnc_updateMissionAttributes

params ["_section","_property","_value"];

if !(missionNamespace getVariable ["EOEX_var_Connected", false]) exitWith {};

// TODO Verify update. Recover if needed and retry.
["SetMissionAttribute", _this, true] call EOEX_fnc_callExtensionAsync;
