


params ["_object"];

/*
	_object = ((get3DENSelected "")#0#0);
	_id = call EXT_fnc_getId;
	_attributes = (_object get3DENAttributes ""); 
	["CreateObject", [_id, _attributes], true] call EXT_fnc_callExtensionAsync;
*/
// ADD ID AND REGISTER TO LIST
_object setVariable ["EOE_var_objectID", nil];
EOE_var_objects deleteAt _object;

//TODO MAKE SURE IS CONNECTED TO SERVER

params [["_function","",[""]],["_arguments",[],[[]]],["_fireAndForget",false,[false]],["_timeout",1,[0]]];


_attributes = (_object get3DENAttributes "");
["CreateObject", [_id, _attributes], true] call EXT_fnc_callExtensionAsync;