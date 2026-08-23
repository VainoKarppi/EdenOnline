// EOEX_fnc_createObject

params ["_object"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

/*
TODO WHEN CREATING A TRIGGER:

[RPT] 21:10:06 REQUEST ASYNC: (CreateObject|88000) WITH ARGS: ["LM1FJHVI",[["Position",[3931.03,3273.39,0]],["ItemClass",<null>]]]
[RPT] 21:10:06 Error in expression <[["Position", _position], ["ItemClass", _class]]]] call EOEX_fnc_callExtensionAs>
[RPT] 21:10:06   Error position: <_class]]]] call EOEX_fnc_callExtensionAs>
[RPT] 21:10:06   Error Undefined variable in expression: _class
[RPT] 21:10:06 File extension\functions\objects\fn_createObject.sqf..., line 20
[RPT] 21:10:06 SUCCESS: (CreateObject|88000): WITH DATA: ["LM1FJHVI"]
*/

// Make sure EOEX_var_objectID gets set, before testing if this object has been already added (created by incoming packet)
// TODO add similiar system as EOEX_var_SkipAttributeChange
uiSleep 0.02;

_id = _object getVariable "EOEX_var_objectID";
if !(isNil "_id") exitWith {};

_id = _object call EOEX_fnc_getId;


private _class = (_object get3DENAttribute "ItemClass") select 0;
private _position = (_object get3DENAttribute "Position") select 0;

["CreateObject", [_id, [["Position", _position], ["ItemClass", _class]]]] call EOEX_fnc_callExtensionAsync;
