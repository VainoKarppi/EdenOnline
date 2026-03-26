

params ["_entity", "_property"];

if !(missionNamespace getVariable ["EXT_var_Connected", false]) exitWith {
	["CONNECT OR START SERVER FIRST!", 0, 5] call BIS_fnc_3DENNotification;
};

if (_entity in allGroups) exitWith {};

// Event was triggered by incoming update from another client
if (_entity getVariable ["EXT_updateRequested", false]) exitWith {
	_entity setVariable ["EXT_updateRequested", nil];
};
_entity setVariable ["EXT_updateRequested", nil];

private _id = _entity call EXT_fnc_getId;
if (_id == "" || isNil "_id") exitWith {diag_log "1"};


private _value = (_entity get3DENAttribute _property) select 0;

if (isNil "_value") exitWith {diag_log format ["%1 %2", _property, _value]};

private _queue = EXT_var_AttributeQueues getOrDefault [_id, createHashMap, true];

_queue set [_property, _value];

// Debounce timer
private _timer = EXT_var_AttributeTimers get _id;
if (!isNil "_timer" && {!scriptDone _timer}) then { terminate _timer; };


_timer = [_id] spawn {
	params ["_id"];
	uiSleep 0.01; // Allow time to queue

	private _queue = EXT_var_AttributeQueues getOrDefault [_id, createHashMap];
	if (count _queue == 0) exitWith {};

	["UpdateObject", [_id, _queue], true] call EXT_fnc_callExtensionAsync;

	EXT_var_AttributeQueues deleteAt _id;
	EXT_var_AttributeTimers deleteAt _id;
};

EXT_var_AttributeTimers set [_id, _timer];