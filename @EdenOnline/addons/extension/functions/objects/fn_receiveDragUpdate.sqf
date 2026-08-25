// EOEX_fnc_receiveDragUpdate

params ["_moveId", "_delta"];

if !(missionNamespace getVariable ["Eden_RemoteMoveActive", false]) exitWith {};

private _activeMoveId = missionNamespace getVariable ["Eden_RemoteMoveId", -1];

if !(_moveId isEqualTo _activeMoveId) exitWith {};

private _objects = missionNamespace getVariable ["Eden_RemoteMoveObjects", []];

private _targets = [];

{
    private _object = _x select 0;
    private _initialPosition = _x select 1;

    private _targetPosition = _initialPosition vectorAdd _delta;

    _targets pushBack [_object, _targetPosition];
} forEach _objects;

missionNamespace setVariable ["Eden_RemoteMoveTargets", _targets];

diag_log format ["MOVE_UPDATE: %1 %2", _moveId, _delta];
