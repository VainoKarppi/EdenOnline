// EOEX_fnc_sendObjectAttributes

// TODO Updating Unit/Vehicle ItemClass removes the object!

params ["_entity", "_property"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

if (_entity isEqualType grpNull) exitWith {};

diag_log _this;
private _type = [_entity, typeName _entity] call EOEX_fnc_getObjectType;

if (_type == "Marker" && _property != "ItemClass") then {
	[_entity, "ItemClass"] spawn EOEX_fnc_sendObjectAttributes; // Always update ItemClass for markers, since this cannot be detected by the eventhandler
};

// Ignore if the entity is a group
if (_type == "Group") exitWith {};

// TODO WAYPOINTS

private _id = [_entity, true] call EOEX_fnc_getId;
if (isNil "_id" || {_id == ""}) exitWith {};

// Get value of the attribute that was changed.
private _value = (_entity get3DENAttribute _property) select 0;
if (isNil "_value") exitWith {};

// Check if the edit has to be skipped (e.g. because it was triggered by an incoming update from another client)
// Added via: [id, [property, value]] call EOEX_fnc_addSkipAttributeChange
private _skip = [_id, [_property, _value]] call EOEX_fnc_checkSkipAttributeChange;
if (_skip) exitWith { diag_log format ["SKIPPING (%1) INCOMING CHANGE: %2, (%3)", _id, _property, _value] };


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
