// EOEX_fnc_onEntityDragStart

// Called from the OnEntityDragged 3DEN event handler.
// Tracks a move operation (START_MOVE + periodic MOVE_UPDATE at ~10 Hz).

params ["_entity"];



// Eden reports the group instead of the unit that was grabbed (same as OnEditableEntityAdded), and
// a group has no Position of its own: get3DENAttribute returns [nil].
if (_entity isEqualType grpNull) then {
    _entity = leader _entity;
};

if (_entity isEqualType objNull && {isNull _entity}) exitWith {};

private _now = diag_tickTime;
private _positionResults = _entity get3DENAttribute "Position";
private _position = if (_positionResults isEqualType [] && {_positionResults isNotEqualTo []}) then { _positionResults # 0 } else { nil };

// Nothing to stream for an entity without a position (e.g. an empty group).
if (isNil "_position") exitWith {};

// Another client holds the entity (its attributes window is open, or it is being dragged there):
// do not start a move here, and put the entity back where it was when the lock was taken.
private _entityId = [_entity, true] call EOEX_fnc_getId;
private _lock = if (isNil "_entityId") then { [] } else { (missionNamespace getVariable ["EOEX_var_Locks", createHashMap]) getOrDefault [_entityId, []] };

if !(_lock isEqualTo []) exitWith {
    _lock params ["_lockedEntity", "_username", "_mode", "_lockedPosition"];

    [format ["%1 is editing %2 (%3). You cannot move it until the lock is released.", _username, _entityId, _mode], 1, 6] call BIS_fnc_3DENNotification;

    if (!isNil "_lockedPosition") then {
        [_entityId, ["Position", _lockedPosition]] call EOEX_fnc_addSkipAttributeChange;

        ignore3DENHistory {
            _entity set3DENAttribute ["Position", _lockedPosition];
        };
    };
};

// Start a new move operation
if !(missionNamespace getVariable ["EOEX_var_MoveActive", false]) then {
    private _moveId = [_entity, false] call EOEX_fnc_getId;

    // Only entities that are on the network have an ID.
    if (isNil "_moveId") exitWith {};

    // Query each type separately: get3DENSelected "" nests the categories, and flattening would
    // break waypoint references into a group and an index.
    private _entities = [];
    {
        _entities append (get3DENSelected _x);
    } forEach ["object", "trigger", "logic", "waypoint", "marker", "comment"];

    // A group can be reported as well, but it has no ID and no position.
    _entities = _entities select { !(_x isEqualType grpNull) };

    if (_entities isEqualTo []) then {
        _entities = [_entity];
    };

    private _objectIds = [];

    {
        private _id = [_x, false] call EOEX_fnc_getId;

        if (!isNil "_id") then { _objectIds pushBack _id };
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
    ["StartObjectDrag", [_moveId, _objectIds, profileNameSteam], true] spawn EOEX_fnc_callExtensionAsync;
};


// Send movement updates at 10 Hz. OnEntityDragged may fire only once, so fn_sendObjectAttributes
// calls this function again whenever the dragged entity's position changes.
private _lastUpdate = missionNamespace getVariable ["EOEX_var_MoveLastUpdate", 0];

if ((_now - _lastUpdate) < EOEX_var_DragUpdateSpeed) exitWith {};

private _moveId = missionNamespace getVariable ["EOEX_var_MoveId", -1];

private _referencePosition = missionNamespace getVariable ["EOEX_var_MoveReferencePosition", _position];

if (!(_referencePosition isEqualType []) || {count _referencePosition != 3}) then {
    _referencePosition = _position;
};

// Delta is relative to the position when the move started
private _delta = _position vectorDiff _referencePosition;

// Send MOVE_UPDATE via UDP
["UpdateObjectDrag", [_moveId, _delta], true] spawn EOEX_fnc_callExtensionAsync;

missionNamespace setVariable ["EOEX_var_MoveLastUpdate", _now];
