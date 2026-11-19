// EOEX_fnc_receiveCreateSyncConnection
params ["_fromID", "_toID", "_type"];


private _fromObject = EOEX_var_Objects getOrDefault [_fromID, objNull];
private _toObject = EOEX_var_Objects getOrDefault [_toID, objNull];


// Validate source
if (isNull _fromObject) exitWith {
    diag_log format ["[EdenOnline] Failed to receive SyncConnection: source object not found: %1", _fromID];
};


// Validate target
if (isNull _toObject) exitWith {
    diag_log format ["[EdenOnline] Failed to receive SyncConnection: target object not found: %1", _toID];
};


// Currently only supporting normal Eden Sync connections.
if (_type != "Sync") exitWith {
    diag_log format ["[EdenOnline] Unsupported received connection type: %1", _type];
};


// Create the Eden connection.
ignore3DENHistory {
    add3DENConnection [_type,[_fromObject],_toObject];
};

// Check whether Eden accepted the connection.
private _success = false;
{
    if ((_x select 0) != _type) then { continue };
    if (_x select 1 == _toObject) exitWith { _success = true };
} forEach get3DENConnections _fromObject;

if !(_success) exitWith {
    diag_log format ["[EdenOnline] Failed to add SyncConnection: %1 (%2) -> %3 (%4)", _fromID, _fromObject, _toID, _toObject];
};
