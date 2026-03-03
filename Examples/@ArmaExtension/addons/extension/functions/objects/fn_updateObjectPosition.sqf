params ["_object"];

_position = (_object get3DENAttribute "position") select 0;

diag_log format ["UPDATING POSITION %1", _position];

/*
	_object = ((get3DENSelected "")#0#0);
	_id = call EXT_fnc_getId;
	_attributes = (_object get3DENAttributes ""); 
	["CreateObject", [_id, _attributes], true] call EXT_fnc_callExtensionAsync;
*/
// ADD ID AND REGISTER TO LIST
_id = call EXT_fnc_getId;

//TODO MAKE SURE IS CONNECTED TO SERVER

// params [["_function","",[""]],["_arguments",[],[[]]],["_fireAndForget",false,[false]],["_timeout",1,[0]]];

// TODO CREW



// hint format ["Entity %1 is in layer %2", typeOf _entity, get3DENLayer _entity];

_position = (_object get3DENAttribute "position") select 0;
systemChat str(_position);
//["UpdateObjectPosition", [_id, _attributes], true] call EXT_fnc_callExtensionAsync;