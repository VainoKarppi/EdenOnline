// EOEX_fnc_receiveDragStart

params ["_moveId", "_objectIds"];



// TODO LOCK OBJECT, SO THAT IT CANNOT BE EDITED

private _objects = [];

private _objectsById = createHashMap;

{
    _objectsById set [_x call EOEX_fnc_getId, _x];
} forEach (all3DENEntities select 0);

{
    private _object = _objectsById getOrDefault [_x, objNull];

    if !(isNull _object) then {
        _objects pushBack [_object, (_object get3DENAttribute "Position") select 0];
    };
} forEach _objectIds;

if (_objects isEqualTo []) exitWith {
    diag_log format ["MOVE_START: No objects found for move %1", _moveId];
};

missionNamespace setVariable ["Eden_RemoteMoveActive", true];
missionNamespace setVariable ["Eden_RemoteMoveId", _moveId];
missionNamespace setVariable ["Eden_RemoteMoveObjects", _objects];

private _targets = [];

{
    _targets pushBack [_x select 0, _x select 1];
} forEach _objects;

missionNamespace setVariable ["Eden_RemoteMoveTargets", _targets ];

diag_log format ["MOVE_START: %1 %2", _moveId, _objectIds];
