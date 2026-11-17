// EOEX_fnc_sendCreateObject

// This file handles types Object, Trigger and Logic
// ItemClass, might not exist, for all objects, but we force it in the attributes

params ["_id", "_object","_type"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

private _position = (_object get3DENAttribute "Position") select 0;

["CreateObject", [_id, _type, [["Position", _position], ["ItemClass", typeOf _object]]]] call EOEX_fnc_callExtensionAsync;
