// EOEX_fnc_receiveLocksReleased

// A client disconnected: every lock it held is stale and has to be dropped here.

params [["_username", "", [""]]];

if (_username isEqualTo "" || {isNil "EOEX_var_Locks"}) exitWith {};

private _stale = (keys EOEX_var_Locks) select { ((EOEX_var_Locks get _x) # 1) isEqualTo _username };

if (_stale isEqualTo []) exitWith {};

{
    [_x] call EOEX_fnc_unlockEntity;
} forEach _stale;

[format ["%1 disconnected. %2 lock(s) released.", _username, count _stale], 0, 5] call BIS_fnc_3DENNotification;
