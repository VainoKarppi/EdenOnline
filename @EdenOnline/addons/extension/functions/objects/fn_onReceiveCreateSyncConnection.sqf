// EOEX_fnc_onReceiveCreateSyncConnection
params ["_fromID", "_toID", "_type", ["_silent", false]];


if (!_silent) then {
    diag_log format ["[EdenOnline] Received SyncConnection: %1 -> %2 (%3)", _fromID, _toID, _type];
};


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
private _result = add3DENConnection [_type,[_fromObject],_toObject];


// Check whether Eden accepted the connection.
if (!_result) exitWith {
    diag_log format ["[EdenOnline] Failed to add SyncConnection: %1 (%2) -> %3 (%4)", _fromID, _fromObject, _toID, _toObject];
};


// Keep the local registry in sync without the O(n) array duplicate scan.
private _connection = [_fromID, _toID, _type];
private _connectionKey = str _connection;
if !(EOEX_var_SyncConnectionKeys getOrDefault [_connectionKey, false]) then {
    EOEX_var_SyncConnections pushBack _connection;
    EOEX_var_SyncConnectionKeys set [_connectionKey, true];
};


if (!_silent) then {
    diag_log format ["[EdenOnline] SyncConnection added locally: %1 (%2) -> %3 (%4)", _fromID, _fromObject, _toID, _toObject];
};
