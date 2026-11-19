// EOEX_fnc_onEntityDragStart

// Called from the OnEntityDragged 3DEN event handler.
// Tracks a move operation (START_MOVE + periodic MOVE_UPDATE at ~10 Hz).

params ["_entity"];



private _now = diag_tickTime;
private _position = (_entity get3DENAttribute "Position") select 0;

// Start a new move operation
if !(missionNamespace getVariable ["EOEX_var_MoveActive", false]) then {
    private _moveId = [_entity, false] call EOEX_fnc_getId;

    // Query each type separately: get3DENSelected "" nests the categories, and flattening would
    // break waypoint references into a group and an index.
    private _entities = [];
    {
        _entities append (get3DENSelected _x);
    } forEach ["object", "trigger", "logic", "waypoint", "marker", "comment"];

    if (_entities isEqualTo []) then {
        _entities = [_entity];
    };

    private _objectIds = [];

    {
        _objectIds pushBack ([_x, false] call EOEX_fnc_getId);
    } forEach _entities;

    // Pending attribute updates describe the pre-drag position; drop them so they cannot overwrite
    // the position that EndObjectDrag sends later.
    {
        private _timer = EOEX_var_AttributeTimers get _x;

        if (!isNil "_timer" && {!scriptDone _timer}) then { terminate _timer };

        EOEX_var_AttributeQueues deleteAt _x;
        EOEX_var_AttributeTimers deleteAt _x;
    } forEach _objectIds;

    missionNamespace setVariable ["EOEX_var_MoveActive", true];
    missionNamespace setVariable ["EOEX_var_MoveId", _moveId];
    missionNamespace setVariable ["EOEX_var_MoveIds", _objectIds];
    missionNamespace setVariable ["EOEX_var_MoveEntities", _entities];
    missionNamespace setVariable ["EOEX_var_MoveReferencePosition", _position];
    missionNamespace setVariable ["EOEX_var_MoveLastUpdate", _now];

    // Send START_MOVE via TCP
    ["StartObjectDrag", [_moveId, _objectIds], true] spawn EOEX_fnc_callExtensionAsync;
};


// Send movement updates at 10 Hz. OnEntityDragged may fire only once, so fn_sendObjectAttributes
// calls this function again whenever the dragged entity's position changes.
private _lastUpdate = missionNamespace getVariable ["EOEX_var_MoveLastUpdate", 0];

if ((_now - _lastUpdate) < 0.1) exitWith {};

private _moveId = missionNamespace getVariable ["EOEX_var_MoveId", -1];

private _referencePosition = missionNamespace getVariable ["EOEX_var_MoveReferencePosition", _position];

// Delta is relative to the position when the move started
private _delta = _position vectorDiff _referencePosition;

// Send MOVE_UPDATE via UDP
["UpdateObjectDrag", [_moveId, _delta], true] spawn EOEX_fnc_callExtensionAsync;

missionNamespace setVariable ["EOEX_var_MoveLastUpdate", _now];
