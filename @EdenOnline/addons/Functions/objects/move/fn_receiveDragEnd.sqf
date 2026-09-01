// EOEX_fnc_receiveDragEnd

params ["_moveId", "_finalPositions"];

if !(missionNamespace getVariable ["Eden_RemoteMoveActive", false]) exitWith {};

private _activeMoveId = missionNamespace getVariable ["Eden_RemoteMoveId", -1];

if (_moveId isNotEqualTo _activeMoveId) exitWith {};

{
    private _objectId = _x select 0;
    private _finalPosition = _x select 1;

    private _object = objNull;

    {
        if ((_x call EOEX_fnc_getId) isEqualTo _objectId) exitWith {
            _object = _x;
        };
    } forEach (all3DENEntities select 0);

    if !(isNull _object) then {
        _object set3DENAttribute [
            "Position",
            _finalPosition
        ];
    };
} forEach _finalPositions;

diag_log format ["MOVE_END: %1 %2", _moveId, _finalPositions];

missionNamespace setVariable ["Eden_RemoteMoveActive", false];
missionNamespace setVariable ["Eden_RemoteMoveId", -1];
missionNamespace setVariable ["Eden_RemoteMoveObjects", []];
missionNamespace setVariable ["Eden_RemoteMoveTargets", []];
