

params ["_entity", "_property"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

if (_entity in allGroups) exitWith {};

// Event was triggered by incoming update from another client
if (_entity getVariable ["EOEX_updateRequested", false]) exitWith {
	_entity setVariable ["EOEX_updateRequested", nil];
};
_entity setVariable ["EOEX_updateRequested", nil];

private _id = _entity call EOEX_fnc_getId;
if (_id == "" || isNil "_id") exitWith {};


private _value = (_entity get3DENAttribute _property) select 0;

if (isNil "_value") exitWith {};

private _queue = EOEX_var_AttributeQueues getOrDefault [_id, createHashMap, true];

_queue set [_property, _value];

// Debounce timer
private _timer = EOEX_var_AttributeTimers get _id;
if (!isNil "_timer" && {!scriptDone _timer}) then { terminate _timer; };


_timer = [_id] spawn {
	params ["_id"];
	uiSleep 0.01; // Allow time to queue

	private _queue = EOEX_var_AttributeQueues getOrDefault [_id, createHashMap];
	if (count _queue == 0) exitWith {};

	["UpdateObject", [_id, _queue], true] call EOEX_fnc_callExtensionAsync;

	EOEX_var_AttributeQueues deleteAt _id;
	EOEX_var_AttributeTimers deleteAt _id;
};

EOEX_var_AttributeTimers set [_id, _timer];