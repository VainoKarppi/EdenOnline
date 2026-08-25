// EOEX_fnc_onEntityDragStart

// Called from the OnEntityDragged 3DEN event handler.
// Tracks a move operation (START_MOVE + periodic MOVE_UPDATE at ~10 Hz).

params ["_entity"];

if !(_entity isEqualType objNull) exitWith {};

private _now = diag_tickTime;
private _position = (_entity get3DENAttribute "Position") select 0;

// Start a new move operation
if !(missionNamespace getVariable ["Eden_MoveActive", false]) then {
    private _moveId = _entity call EOEX_fnc_getId;
    private _entities = get3DENSelected "object";

    if (_entities isEqualTo []) then {
        _entities = [_entity];
    };

    private _objectIds = [];

    {
        _objectIds pushBack (_x call EOEX_fnc_getId);
    } forEach _entities;

    missionNamespace setVariable ["Eden_MoveActive", true];
    missionNamespace setVariable ["Eden_MoveId", _moveId];
    missionNamespace setVariable ["Eden_MoveEntities", _entities];
    missionNamespace setVariable ["Eden_MoveReferencePosition", _position];
    missionNamespace setVariable ["Eden_MoveLastUpdate", _now];

    // Send START_MOVE via TCP
    ["StartObjectDrag", [_moveId, _objectIds], true] spawn EOEX_fnc_callExtensionAsync;
};


// Send movement updates at 10 Hz
private _lastUpdate = missionNamespace getVariable ["Eden_MoveLastUpdate", 0];

if ((_now - _lastUpdate) < 0.1) exitWith {};

private _moveId = missionNamespace getVariable ["Eden_MoveId", -1];

private _referencePosition = missionNamespace getVariable ["Eden_MoveReferencePosition", _position];

// Delta is relative to the position when the move started
private _delta = _position vectorDiff _referencePosition;

// Send MOVE_UPDATE via UDP
["UpdateObjectDrag", [_moveId, _delta], true] spawn EOEX_fnc_callExtensionAsync;

missionNamespace setVariable ["Eden_MoveLastUpdate", _now];
