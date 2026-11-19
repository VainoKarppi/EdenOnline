// EOEX_fnc_receiveLocksReleased

// A client disconnected, or connected again (a reconnect registers a new client id): every lock it
// held is stale and has to be dropped here. Locks are matched by client id, so two clients that use
// the same profile name do not release each other. Drag locks carry no client id (-1) and fall back
// to the name.

params [["_clientId", -1, [0]], ["_username", "", [""]]];

if (isNil "EOEX_var_Locks" || {(count EOEX_var_Locks) == 0}) exitWith {};

private _stale = (keys EOEX_var_Locks) select {
    private _entry = EOEX_var_Locks get _x;
    private _holderId = _entry param [4, -1];

    if (!(_holderId isEqualType 0)) then { _holderId = -1 };

    (_holderId == _clientId) || {(_holderId < 0) && {(_entry # 1) isEqualTo _username}}
};

if (_stale isEqualTo []) exitWith {};

{
    [_x] call EOEX_fnc_unlockEntity;
} forEach _stale;

[format ["%1 released %2 lock(s).", _username, count _stale], 0, 5] call BIS_fnc_3DENNotification;
