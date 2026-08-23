// EOEX_fnc_onReceiveRemoveSyncConnection
params ["_fromID", "_toID", "_type"];


if (EOEX_var_DEBUG) then {
    diag_log format ["[EdenOnline] Received RemoveSyncConnection: %1 -> %2 (%3)", _fromID, _toID, _type];
};


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
private _result = remove3DENConnection [_type, [_fromObject], _toObject];


// Check result
if (!_result) exitWith {
    diag_log format ["[EdenOnline] Failed to remove SyncConnection: %1 (%2) -> %3 (%4)", _fromID, _fromObject, _toID, _toObject];
};


private _connection = [_fromID, _toID, _type];
private _connectionIndex = EOEX_var_SyncConnections find _connection;
if (_connectionIndex != -1) then { EOEX_var_SyncConnections deleteAt _connectionIndex };
EOEX_var_SyncConnectionKeys deleteAt (str _connection);

if (EOEX_var_DEBUG) then {
    diag_log format ["[EdenOnline] SyncConnection removed locally: %1 (%2) -> %3 (%4)", _fromID, _fromObject, _toID, _toObject];
};
