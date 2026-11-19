// EOEX_fnc_lockEntity

// Marks an entity as locked by another client. The lock is only a local mirror: the client that
// holds the lock renews it while it keeps the attributes window open (StartAttributesEdit) and
// removes it with StopAttributesEdit / EndObjectDrag, on disconnect (LocksReleased) or by expiry.
//
// A locked entity cannot be dragged and its attributes window cannot be opened here (see
// fn_onEntityDragStart, fn_enforceLocks and fn_watchAttributesEdit).
//
// Entry: id -> [entity, username, mode, position, timestamp]
//   mode       "attributes" (attributes window open) or "drag" (being dragged)
//   timestamp  diag_tickTime of the last renewal; "attributes" locks expire without one

params [["_id", "", [""]], ["_username", "another client", [""]], ["_mode", "attributes", [""]]];

if (_id isEqualTo "") exitWith {};

if (isNil "EOEX_var_Locks") then {
    EOEX_var_Locks = createHashMap;
};

private _existing = EOEX_var_Locks getOrDefault [_id, []];

// Same holder and mode: only the heartbeat is renewed, the entity must stay where it was locked.
if (!(_existing isEqualTo []) && {(_existing # 1) isEqualTo _username} && {(_existing # 2) isEqualTo _mode}) exitWith {
    if ((_existing # 4) isEqualType 0) then { _existing set [4, diag_tickTime] };
};

private _entity = [_id] call EOEX_fnc_getEntityById;

// Unknown id: the entity is not created here (yet), so there is nothing to lock.
if (isNil "_entity") exitWith {};

private _position = (_entity get3DENAttribute "Position") param [0, nil];

EOEX_var_Locks set [_id, [_entity, _username, _mode, _position, diag_tickTime]];

// Draws the indicator for every lock from a single handler.
call EOEX_fnc_drawLocks;

// Keeps locked entities where they are and out of the local selection.
call EOEX_fnc_enforceLocks;
