// EOEX_fnc_sendObjectAttributes


params ["_entity", "_property"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

// Ignore if the entity is a group
if (_entity in allGroups) exitWith {};

// Get the unique ID of the object or marker
private _id = _entity call EOEX_fnc_getId;
if (isNil "_id" || {_id == ""}) exitWith {};

// Get value of the attribute that was changed.
private _value = (_entity get3DENAttribute _property) select 0;
if (isNil "_value") exitWith {};

// Check if the edit has to be skipped (e.g. because it was triggered by an incoming update from another client)
// Added via: [id, [property, value]] call EOEX_fnc_addSkipAttributeChange
private _skip = [_id, [_property, _value]] call EOEX_fnc_checkSkipAttributeChange;
if (_skip) exitWith { diag_log format ["SKIP: %1, %2, %3", _id, _property, _value] };



// TODO check if marker
if (_entity isEqualType "") exitWith {
	// TODO queue marker attribute updates to avoid sending multiple updates in a single frame
	// TODO send data to server
	diag_log _entity;
	diag_log _property;
	diag_log "marker edited";
};

// Event was triggered by incoming update from another client
if (_entity getVariable ["EOEX_updateRequested", false]) exitWith {
	_entity setVariable ["EOEX_updateRequested", nil];
};
_entity setVariable ["EOEX_updateRequested", nil];






private _queue = EOEX_var_AttributeQueues getOrDefault [_id, createHashMap, true];

_queue set [_property, _value];

// Debounce timer
private _timer = EOEX_var_AttributeTimers get _id;
if (!isNil "_timer" && {!scriptDone _timer}) then { terminate _timer; };


_timer = [_id, _entity] spawn {
	params ["_id", "_entity"];
	sleep 0.02; // Allow time for attribute change events to arrive across client frames before reading the final state.

	private _queue = EOEX_var_AttributeQueues getOrDefault [_id, createHashMap];
	if (count _queue == 0) exitWith {};

	private _type = _entity call EOEX_fnc_getObjectType;
	if (isNil "_type") exitWith { diag_log format ["ERROR: Object type not found for ID: %1", _id] };

	["UpdateObject", [_id, _type, _queue], true] call EOEX_fnc_callExtensionAsync;

	EOEX_var_AttributeQueues deleteAt _id;
	EOEX_var_AttributeTimers deleteAt _id;
};

EOEX_var_AttributeTimers set [_id, _timer];
