// EOEX_fnc_sendObjectAttributes

// TODO Updating Unit/Vehicle ItemClass removes the object!

params ["_entity", "_property"];

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};


if (_entity isEqualType grpNull) exitWith {};

private _type = [_entity, typeName _entity] call EOEX_fnc_getObjectType;

// Ignore if the entity is a group
if (_type == "Group") exitWith {};

private _id = [_entity, true] call EOEX_fnc_getId;
if (isNil "_id" || {_id == ""}) exitWith {};

// Get value of the attribute that was changed.
private _value = (_entity get3DENAttribute _property) select 0;
if (isNil "_value") exitWith {};

// Check if the edit has to be skipped (e.g. because it was triggered by an incoming update from another client)
// Added via: [id, [property, value]] call EOEX_fnc_addSkipAttributeChange
private _skip = [_id, [_property, _value]] call EOEX_fnc_checkSkipAttributeChange;
if (_skip) exitWith {};

// A remote client is dragging this entity: fn_moveInterpolation writes its position every frame.
// Those writes are not user edits and must not be sent back to the client that owns the drag.
if ((missionNamespace getVariable ["EOEX_var_RemoteMoveActive", false]) && {_id in (missionNamespace getVariable ["EOEX_var_RemoteMoveIds", []])}) exitWith {};

// The change events of the final drag writes arrive asynchronously and can still run after the
// move was marked as finished. The dragging client already sent the authoritative final state, so
// nothing that touches the just dragged entities may be echoed back.
if ((missionNamespace getVariable ["EOEX_var_RemoteMoveIgnoreUntil", 0]) > diag_tickTime && {_id in (missionNamespace getVariable ["EOEX_var_RemoteMoveIgnoreIds", []])}) exitWith {};

// This client is dragging the entity. The movement is streamed over UDP (UpdateObjectDrag) and the
// final state is sent by EndObjectDrag, so sending every intermediate Position/Rotation as a TCP
// UpdateObject only floods the connection. Feeding the position changes to onEntityDragStart keeps
// the 10 Hz UDP stream going (OnEntityDragged itself fires only on drag start).
if ((_property == "Position" || {_property == "Rotation"}) && {_id in (missionNamespace getVariable ["EOEX_var_MoveIds", []])}) exitWith {
	if (_property == "Position" && {_id == (missionNamespace getVariable ["EOEX_var_MoveId", ""])}) then {
		[_entity] call EOEX_fnc_onEntityDragStart;
	};
};

// Always update ItemClass for markers, since this cannot be detected by the eventhandler.
// This runs after the skip check, so a received marker attribute change is not echoed back.
if (_type == "Marker" && _property != "ItemClass") then {
	[_entity, "ItemClass"] spawn EOEX_fnc_sendObjectAttributes;
};


private _queue = EOEX_var_AttributeQueues getOrDefault [_id, createHashMap, true];

_queue set [_property, _value];

// Debounce timer
private _timer = EOEX_var_AttributeTimers get _id;
if (!isNil "_timer" && {!scriptDone _timer}) then { terminate _timer; };

// If Position is sent, then always add also Rotation
if (!isNil {_queue get "Position"}) then {
	// Comments and waypoints have no Rotation attribute; the lookup returns an empty array for them.
	private _rotation = (_entity get3DENAttribute "Rotation") param [0, nil];

	if (!isNil "_rotation") then { _queue set ["Rotation", _rotation] };
};

_timer = [_id, _entity] spawn {
	params ["_id", "_entity"];
	sleep 0.02; // Allow time for attribute change events to arrive across client frames before reading the final state.

	private _queue = EOEX_var_AttributeQueues getOrDefault [_id, createHashMap];
	if (count _queue == 0) exitWith {};

	private _type = [_entity, typeName _entity] call EOEX_fnc_getObjectType;
	if (isNil "_type") exitWith { diag_log format ["ERROR: Object type not found for ID: %1", _id] };

	["UpdateObject", [_id, _type, _queue], true] call EOEX_fnc_callExtensionAsync;

	EOEX_var_AttributeQueues deleteAt _id;
	EOEX_var_AttributeTimers deleteAt _id;
};

EOEX_var_AttributeTimers set [_id, _timer];
