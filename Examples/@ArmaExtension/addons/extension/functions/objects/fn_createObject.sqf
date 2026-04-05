
params ["_object"];

// When object is copied, this event will only run for the created unit group (not twice)
diag_log "OBJECT CREATED";
diag_log _object; // GROUP 

if !(missionNamespace getVariable ["EXT_var_Connected",false]) exitWith {
	["CONNECT OR START SERVER FIRST!", 0,5] call BIS_fnc_3DENNotification;
};

if (_object in allGroups) exitWith {}; // Dont sync groups for now...


/*
	_object = ((get3DENSelected "")#0#0);
	_id = call EXT_fnc_getId;
	_attributes = (_object get3DENAttributes ""); 
	["CreateObject", [_id, _attributes], true] call EXT_fnc_callExtensionAsync;
*/
// ADD ID AND REGISTER TO LIST

// Make sure EXT_objectID gets set, before testing if this object has been already added
uiSleep 0.01;

_id = _object getVariable "EXT_objectID";
if !(isNil "_id") exitWith {};

_id = _object call EXT_fnc_getId;


// TODO CREW


// hint format ["Entity %1 is in layer %2", typeOf _entity, get3DENLayer _entity];

private _class = (_object get3DENAttribute "ItemClass") select 0;
private _position = (_object get3DENAttribute "Position") select 0;

["CreateObject", [_id, [["Position", _position], ["ItemClass", _class]]]] call EXT_fnc_callExtensionAsync;

// ["UpdateObject|-1",["2IF1IFLY",[["Rotation",[0,0,92.9759]],["Position",[6347.11,4252.56,5.90472]]]]]
// ["CreateObject|-1",["F9W55J3T",[["Positions",[62.2814,4468.31,0]],["ItemClass","B_soldier_AA_F"]]]]

// _data=[""2IF1IFLY"",[[""Rotation"",[0,0,92.9759]],[""Position"",[6347.11,4252.56,5.90472]]]]"
// _data=[""S3R3WTJX"",[[""Positions"",[6912.68,2208.78,0]],[""ItemClass"",""B_soldier_M_F""]]]"