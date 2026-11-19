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
private _all3DENEntitites = all3DENEntities;
private _isEntityValid = {
    params ["_entity"];

    if (_entity isEqualType objNull) exitWith { !(isNull _entity) };
    if (_entity isEqualType "") exitWith { _entity in (_all3DENEntitites # 5) }; // markers
    if (_entity isEqualType 0) exitWith { _entity in (_all3DENEntitites # 7) }; // comments
    if (_entity isEqualType []) exitWith { (count _entity) == 2 }; // waypoints

    false
};

// Stop the interpolation first: a frame running between the final writes and the flag reset below
// would move the entity away from the final position again.
missionNamespace setVariable ["EOEX_var_RemoteMoveTargets", []];

// The change events of the final writes are dispatched asynchronously, and several of them (one per
// interpolation frame) can read the final value after the move was marked as finished. Ignore all
// attribute changes of these entities for a moment so none of them is echoed back to the dragging
// client, which is the authoritative owner of the final state.
missionNamespace setVariable ["EOEX_var_RemoteMoveIgnoreIds", _moved apply { _x # 0 }];
missionNamespace setVariable ["EOEX_var_RemoteMoveIgnoreUntil", diag_tickTime + 0.5];

{
    _x params [["_objectId", nil], ["_finalPosition", nil], ["_finalRotation", nil]];

    if (isNil "_objectId") then { continue };

    private _entity = _entitiesById getOrDefault [_objectId, nil];

    if (isNil "_entity") then {
        diag_log format ["MOVE_END: %1 was not part of move %2", _objectId, _moveId];
        continue;
    };

    if ([_entity] call _isEntityValid) then {
        // The final writes must not be echoed back to the client that dragged the entity. The
        // drag guard in fn_sendObjectAttributes covers the streamed frames, but these writes can
        // raise their change events after the move was already marked as finished.
        if (!isNil "_finalPosition") then {
            [_objectId, ["Position", _finalPosition]] call EOEX_fnc_addSkipAttributeChange;
            _entity set3DENAttribute ["Position", _finalPosition];
        };
        
        if (!isNil "_finalRotation") then {
            [_objectId, ["Rotation", _finalRotation]] call EOEX_fnc_addSkipAttributeChange;
            _entity set3DENAttribute ["Rotation", _finalRotation];
        };
    };
} forEach _finalPositions;

missionNamespace setVariable ["EOEX_var_RemoteMoveActive", false];
missionNamespace setVariable ["EOEX_var_RemoteMoveId", ""];
missionNamespace setVariable ["EOEX_var_RemoteMoveIds", []];
missionNamespace setVariable ["EOEX_var_RemoteMoveObjects", []];
missionNamespace setVariable ["EOEX_var_RemoteMoveTargets", []];
