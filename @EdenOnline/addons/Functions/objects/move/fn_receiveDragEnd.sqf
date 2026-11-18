// EOEX_fnc_receiveDragEnd

params ["_moveId", "_finalPositions"];

if !(missionNamespace getVariable ["EOEX_var_RemoteMoveActive", false]) exitWith {};

private _activeMoveId = missionNamespace getVariable ["EOEX_var_RemoteMoveId", -1];

if (_moveId isNotEqualTo _activeMoveId) exitWith {};

private _moved = missionNamespace getVariable ["EOEX_var_RemoteMoveObjects", []];

private _entitiesById = createHashMap;

{
    _entitiesById set [_x # 0, _x # 1];
} forEach _moved;

// Objects, markers (names), comments (indices) and waypoints ([group, index]) must be checked
// differently: only objects may be passed to isNull.
private _isEntityValid = {
    params ["_entity"];

    if (_entity isEqualType objNull) exitWith { !(isNull _entity) };
    if (_entity isEqualType "") exitWith { _entity in (all3DENEntities # 5) }; // markers
    if (_entity isEqualType 0) exitWith { _entity in (all3DENEntities # 7) }; // comments
    if (_entity isEqualType []) exitWith { (count _entity) == 2 }; // waypoints

    false
};

{
    _x params [["_objectId", nil], ["_finalPosition", nil]];

    if (isNil "_objectId" || {isNil "_finalPosition"}) then { continue };

    private _entity = _entitiesById getOrDefault [_objectId, nil];

    if (isNil "_entity") then {
        diag_log format ["MOVE_END: %1 was not part of move %2", _objectId, _moveId];
        continue;
    };

    if ([_entity] call _isEntityValid) then {
        _entity set3DENAttribute ["Position", _finalPosition];
    };
} forEach _finalPositions;

missionNamespace setVariable ["EOEX_var_RemoteMoveActive", false];
missionNamespace setVariable ["EOEX_var_RemoteMoveId", -1];
missionNamespace setVariable ["EOEX_var_RemoteMoveObjects", []];
missionNamespace setVariable ["EOEX_var_RemoteMoveTargets", []];
