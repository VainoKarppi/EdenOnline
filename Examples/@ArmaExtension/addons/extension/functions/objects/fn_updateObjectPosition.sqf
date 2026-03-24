params ["_object"];

if !(missionNamespace getVariable ["EXT_var_Connected",false]) exitWith {
	["CONNECT OR START SERVER FIRST!", 0,5] call BIS_fnc_3DENNotification;
};

//TODO SEND ONLY AFTER FINAL POSITION && every 10 tick???
// TODO SEND USING UDP???

_id = _object call EXT_fnc_getId;

//TODO MAKE SURE IS CONNECTED TO SERVER

// params [["_function","",[""]],["_arguments",[],[[]]],["_fireAndForget",false,[false]],["_timeout",1,[0]]];

// TODO CREW



// hint format ["Entity %1 is in layer %2", typeOf _entity, get3DENLayer _entity];

_position = (_object get3DENAttribute "position") select 0;
if (isNil "_position") exitWith { systemChat "ERROR" };

systemChat str([_id, _position]);

// SEND POSITION UPDATE OVER UDP. TCP = final position
//["UpdateObjectPosition", [_id, _attributes], true] call EXT_fnc_callExtensionAsync;