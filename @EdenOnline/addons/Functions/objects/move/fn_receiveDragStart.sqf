// EOEX_fnc_receiveDragStart

params ["_moveId", "_objectIds"];



// TODO LOCK OBJECT, SO THAT IT CANNOT BE EDITED

// Every registry maps an EOEX id to its entity, so a drag can carry objects, triggers, logics,
// markers, waypoints and comments. Markers are stored by name and waypoints as [group, index],
// they are kept as they are and never passed to isNull.
private _entitiesById = createHashMap;

{
    private _registry = _x;
    { _entitiesById set [_x, _y] } forEach _registry;
} forEach [EOEX_var_Objects, EOEX_var_Markers, EOEX_var_Waypoints, EOEX_var_Comments];

private _moved = [];

{
    private _entity = _entitiesById getOrDefault [_x, nil];

    if (isNil "_entity") then {
        diag_log format ["MOVE_START: %1 was not found for move %2", _x, _moveId];
        continue;
    };

    private _position = (_entity get3DENAttribute "Position") param [0, nil];

    if (isNil "_position") then {
        diag_log format ["MOVE_START: %1 has no position for move %2", _x, _moveId];
        continue;
    };

    _moved pushBack [_x, _entity, _position];
} forEach _objectIds;

if (_moved isEqualTo []) exitWith {
    diag_log format ["MOVE_START: No entities found for move %1", _moveId];
};

missionNamespace setVariable ["Eden_RemoteMoveActive", true];
missionNamespace setVariable ["Eden_RemoteMoveId", _moveId];
missionNamespace setVariable ["Eden_RemoteMoveObjects", _moved];

private _targets = _moved apply { [_x # 1, _x # 2] };

missionNamespace setVariable ["Eden_RemoteMoveTargets", _targets];
