

params ["_entity", "_property"];

if !(missionNamespace getVariable ["EXT_var_Connected", false]) exitWith {
	["CONNECT OR START SERVER FIRST!", 0, 5] call BIS_fnc_3DENNotification;
};

private _id = _entity call EXT_fnc_getId;
if (_id == "" || isNil "_id") exitWith {};


private _value = (_entity get3DENAttribute _property) select 0;

if (isNil "_value") exitWith {};

private _queue = EXT_var_AttributeQueues getOrDefault [_id, createHashMap, true];

_queue set [_property, _value];

// Debounce timer
private _timer = EXT_var_AttributeTimers get _id;
if (!isNil "_timer" && {!scriptDone _timer}) then { terminate _timer; };


_timer = [_id] spawn {
	params ["_id"];
	sleep 0.02; // Allow time to queue

	private _queue = EXT_var_AttributeQueues getOrDefault [_id, createHashMap];
	if (count _queue == 0) exitWith {};

	diag_log format ["[EXT] Batch sending %1 attrs for %2", _queue, _id];
	["UpdateObject", [_id, _queue], true] call EXT_fnc_callExtensionAsync;

	EXT_var_AttributeQueues deleteAt _id;
	EXT_var_AttributeTimers deleteAt _id;
};

EXT_var_AttributeTimers set [_id, _timer];