// EOEX_fnc_receiveRemoveSyncConnection
params ["_fromID", "_toID", "_type"];


private _fromObject = EOEX_var_Objects getOrDefault [_fromID, objNull];
private _toObject = EOEX_var_Objects getOrDefault [_toID, objNull];


// Validate source
if (isNull _fromObject) exitWith {
    diag_log format ["[EdenOnline] Failed to remove SyncConnection: source object not found: %1", _fromID];
};


// Validate target
if (isNull _toObject) exitWith {
    diag_log format ["[EdenOnline] Failed to remove SyncConnection: target object not found: %1", _toID];
};


// Currently only supporting normal Eden Sync connections
if (_type != "Sync") exitWith {
    diag_log format ["[EdenOnline] Unsupported received connection type: %1", _type];
};


// Remove the Eden connection
remove3DENConnection [_type, [_fromObject], _toObject];

// Verify removal
private _exists = false;

{
    if ((_x select 0) != _type) then { continue };
    if ((_x select 1) == _toObject) exitWith { _exists = true };
} forEach get3DENConnections _fromObject;

if (_exists) exitWith {
    diag_log format ["[EdenOnline] Failed to remove SyncConnection: %1 (%2) -> %3 (%4)", _fromID, _fromObject, _toID, _toObject];
};
