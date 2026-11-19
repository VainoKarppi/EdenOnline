// EOEX_fnc_onEntityDragEnd

// Called when the left mouse button is released after dragging objects in 3DEN.
// Sends the authoritative final positions (END_MOVE) and resets move state.

if !(missionNamespace getVariable ["EOEX_var_MoveActive", false]) exitWith {};

private _moveId = missionNamespace getVariable ["EOEX_var_MoveId", -1];

private _entities = missionNamespace getVariable ["EOEX_var_MoveEntities", []];

// Capture authoritative final positions
private _finalPositions = [];

{
    // Groups have no position of their own and cannot be moved directly.
    if (_x isEqualType grpNull) then { continue };

    private _positionResults = _x get3DENAttribute "Position";
    private _position = if (_positionResults isEqualType [] && {!(_positionResults isEqualTo [])}) then { _positionResults # 0 } else { nil };

    private _id = [_x, false] call EOEX_fnc_getId;

    if (!isNil "_position" && {!isNil "_id"}) then {
        private _finalState = [_id, _position];

        // The rotation changes during the drag are not sent as UpdateObject either, so carry the
        // final one here as well (fn_receiveDragEnd applies it as the optional third element).
        private _rotationResults = _x get3DENAttribute "Rotation";
        private _rotation = if (_rotationResults isEqualType [] && {!(_rotationResults isEqualTo [])}) then { _rotationResults # 0 } else { nil };

        if (!isNil "_rotation") then { _finalState pushBack _rotation };

        _finalPositions pushBack _finalState;
    };
} forEach _entities;

// Send END_MOVE via TCP
// TODO We should be able to remove this, as we already send UpdateObject
["EndObjectDrag", [_moveId, _finalPositions], true] spawn EOEX_fnc_callExtensionAsync;

// This client owns the final state of the entities it just dragged. Position/Rotation updates that
// are still in flight from the other clients are echoes of the drag and would only make the object
// snap back, so ignore them for a moment.
missionNamespace setVariable ["EOEX_var_MoveIgnoreIncomingIds", _finalPositions apply { _x # 0 }];
missionNamespace setVariable ["EOEX_var_MoveIgnoreIncomingUntil", diag_tickTime + 0.5];

// Reset move state
missionNamespace setVariable ["EOEX_var_MoveActive", false];
missionNamespace setVariable ["EOEX_var_MoveLastUpdate", 0];
missionNamespace setVariable ["EOEX_var_MoveId", ""];
missionNamespace setVariable ["EOEX_var_MoveIds", []];
missionNamespace setVariable ["EOEX_var_MoveEntities", []];
missionNamespace setVariable ["EOEX_var_MoveReferencePosition", [0, 0, 0]];
