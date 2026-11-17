// EOEX_fnc_onEntityDragEnd

// Called when the left mouse button is released after dragging objects in 3DEN.
// Sends the authoritative final positions (END_MOVE) and resets move state.

if !(missionNamespace getVariable ["Eden_MoveActive", false]) exitWith {};

private _moveId = missionNamespace getVariable ["Eden_MoveId", -1];

private _entities = missionNamespace getVariable ["Eden_MoveEntities", []];

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
["EndObjectDrag", [_moveId, _finalPositions], true] spawn EOEX_fnc_callExtensionAsync;

// Reset move state
missionNamespace setVariable ["Eden_MoveActive", false];
missionNamespace setVariable ["Eden_MoveLastUpdate", 0];
missionNamespace setVariable ["Eden_MoveId", -1];
missionNamespace setVariable ["Eden_MoveEntities", []];
missionNamespace setVariable ["Eden_MoveReferencePosition", [0, 0, 0]];
