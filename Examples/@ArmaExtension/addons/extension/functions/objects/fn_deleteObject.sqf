


params ["_object"];

if !(missionNamespace getVariable ["EXT_var_Connected",false]) exitWith {
	["CONNECT OR START SERVER FIRST!", 0,5] call BIS_fnc_3DENNotification;
};

if (isNull _object || _object in allGroups) exitWith {};

/*
	_object = ((get3DENSelected "")#0#0);
	_id = call EXT_fnc_getId;
	_attributes = (_object get3DENAttributes ""); 
	["CreateObject", [_id, _attributes], true] call EXT_fnc_callExtensionAsync;
*/

// Event was triggered by incoming update from another client
if (_entity getVariable ["EXT_updateRequested", false]) exitWith {
	_entity setVariable ["EXT_updateRequested", nil];
};
_entity setVariable ["EXT_updateRequested", nil];


_id = _object call EXT_fnc_getId;


["RemoveObject", [_id]] spawn EXT_fnc_callExtensionAsync;

//_object setVariable ["EXT_var_objectID", nil];