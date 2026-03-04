
params ["_object"];

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


_id = _object call EXT_fnc_getId;


// params [["_function","",[""]],["_arguments",[],[[]]],["_fireAndForget",false,[false]],["_timeout",1,[0]]];

// TODO CREW


// hint format ["Entity %1 is in layer %2", typeOf _entity, get3DENLayer _entity];

// TODO Move to extension
//_attributes = (_object get3DENAttributes "") select { !isNil {_x#1} };

_attributes = (_object get3DENAttributes "");


["CreateObject", [_id, _attributes]] call EXT_fnc_callExtensionAsync;