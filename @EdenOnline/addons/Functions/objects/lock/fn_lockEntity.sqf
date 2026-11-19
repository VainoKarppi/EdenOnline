// EOEX_fnc_lockEntity

// Marks an entity as locked by another client. The lock is only a local mirror: the client that holds
// it removes it with StopAttributesEdit / EndObjectDrag, and everything it still holds is released
// when it disconnects or registers again (LocksReleased, see
// ClientNetworkEvents.OnOtherClientDisconnected and ClientNetworkMethods.RegisterUserName).
//
// A locked entity cannot be dragged and its attributes window cannot be opened here (see
// fn_onEntityDragStart, fn_enforceLocks and fn_watchAttributesEdit).
//
// Entry: id -> [entity, username, mode, position, clientId]
//   mode      "attributes" (attributes window open) or "drag" (being dragged)
//   clientId  id of the holder, -1 when unknown (drag locks)

params [["_id", "", [""]], ["_username", "another client", [""]], ["_mode", "attributes", [""]], ["_clientId", -1, [0]]];

if (_id isEqualTo "") exitWith {};

if (isNil "EOEX_var_Locks") then {
    EOEX_var_Locks = createHashMap;
};

private _existing = EOEX_var_Locks getOrDefault [_id, []];

// Same holder and mode: the entity must stay where it was when it was locked.
if (!(_existing isEqualTo []) && {(_existing # 1) isEqualTo _username} && {(_existing # 2) isEqualTo _mode}) exitWith {};

private _entity = [_id] call EOEX_fnc_getEntityById;

// Unknown id: the entity is not created here (yet), so there is nothing to lock.
if (isNil "_entity") exitWith {};

private _position = (_entity get3DENAttribute "Position") param [0, nil];

EOEX_var_Locks set [_id, [_entity, _username, _mode, _position, _clientId]];

// Draws the indicator for every lock from a single handler.
call EOEX_fnc_drawLocks;

// Keeps locked entities where they are and out of the local selection.
call EOEX_fnc_enforceLocks;
