// EOEX_fnc_sendMissionAttributes
// EOEX_fnc_updateMissionAttributes

params ["_section","_property","_value"];

if !(missionNamespace getVariable ["EOEX_var_Connected", false]) exitWith {};

["SetMissionAttribute", _this] call EOEX_fnc_callExtensionAsync;
