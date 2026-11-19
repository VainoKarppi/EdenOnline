// EOEX_fnc_receiveAttributesEditStop

params [["_ids", [], [[]]]];

{
    [_x] call EOEX_fnc_unlockEntity;
} forEach _ids;
