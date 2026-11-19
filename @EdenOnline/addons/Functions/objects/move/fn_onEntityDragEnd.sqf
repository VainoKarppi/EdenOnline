// EOEX_fnc_onEntityDragEnd

// Called when the left mouse button is released after dragging objects in 3DEN.
// Sends the authoritative final positions (END_MOVE) and resets move state.

if !(missionNamespace getVariable ["EOEX_var_MoveActive", false]) exitWith {};

private _moveId = missionNamespace getVariable ["EOEX_var_MoveId", -1];

private _entities = missionNamespace getVariable ["EOEX_var_MoveEntities", []];

// Capture authoritative final positions
private _finalPositions = [];

{
    private _position = (_x get3DENAttribute "Position") param [0, nil];

    // Groups have no position of their own and cannot be moved directly.
    if (!isNil "_position") then {
        _finalPositions pushBack [[_x, false] call EOEX_fnc_getId, _position];
    };
} forEach _entities;

// Send END_MOVE via TCP
// TODO We should be able to remove this, as we already send UpdateObject
["EndObjectDrag", [_moveId, _finalPositions], true] spawn EOEX_fnc_callExtensionAsync;

// Reset move state
missionNamespace setVariable ["EOEX_var_MoveActive", false];
missionNamespace setVariable ["EOEX_var_MoveLastUpdate", 0];
missionNamespace setVariable ["EOEX_var_MoveId", -1];
missionNamespace setVariable ["EOEX_var_MoveEntities", []];
missionNamespace setVariable ["EOEX_var_MoveReferencePosition", [0, 0, 0]];
