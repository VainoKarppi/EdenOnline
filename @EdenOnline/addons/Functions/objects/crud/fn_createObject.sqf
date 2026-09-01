// EOEX_fnc_createObject

// This file handles types Object, Trigger and Logic

params ["_object","_type"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

// TODO add similiar system as EOEX_var_SkipAttributeChange
uiSleep 0.02;

private _id = _object getVariable "EOEX_var_objectID";
if !(isNil "_id") exitWith {}; // Already created

_id = _object call EOEX_fnc_getId;

private _position = (_object get3DENAttribute "Position") select 0;

["CreateObject", [_id, _type, [["Position", _position], ["ItemClass", typeOf _object]]]] call EOEX_fnc_callExtensionAsync;
