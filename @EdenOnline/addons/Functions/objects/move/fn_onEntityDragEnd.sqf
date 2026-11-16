// EOEX_fnc_onEntityDragEnd

// Called when the left mouse button is released after dragging objects in 3DEN.
// Sends the authoritative final positions (END_MOVE) and resets move state.

if !(missionNamespace getVariable ["Eden_MoveActive", false]) exitWith {};

private _moveId = missionNamespace getVariable ["Eden_MoveId", -1];

private _entities = missionNamespace getVariable ["Eden_MoveEntities", []];

// Capture authoritative final positions
private _finalPositions = [];

{
    private _objectId = _x call EOEX_fnc_getId;

    private _position = (_x get3DENAttribute "Position") select 0;

    _finalPositions pushBack [_objectId, _position];
} forEach _entities;

// Send END_MOVE via TCP
["EndObjectDrag", [_moveId, _finalPositions], true] spawn EOEX_fnc_callExtensionAsync;

// Reset move state
missionNamespace setVariable ["Eden_MoveActive", false];
missionNamespace setVariable ["Eden_MoveLastUpdate", 0];
missionNamespace setVariable ["Eden_MoveId", -1];
missionNamespace setVariable ["Eden_MoveEntities", []];
missionNamespace setVariable ["Eden_MoveReferencePosition", [0, 0, 0]];
