


params ["_object"];

if !(missionNamespace getVariable ["EXT_var_Connected",false]) exitWith {
	["CONNECT OR START SERVER FIRST!", 0,5] call BIS_fnc_3DENNotification;
};

if (_object in allGroups) exitWith {};

/*
	_object = ((get3DENSelected "")#0#0);
	_id = call EXT_fnc_getId;
	_attributes = (_object get3DENAttributes ""); 
	["CreateObject", [_id, _attributes], true] call EXT_fnc_callExtensionAsync;
*/
// ADD ID AND REGISTER TO LIST
_id = _object call EXT_fnc_getId;



//TODO MAKE SURE IS CONNECTED TO SERVER


["RemoveObject", [_id], true] call EXT_fnc_callExtensionAsync;

_object setVariable ["EXT_var_objectID", nil];
