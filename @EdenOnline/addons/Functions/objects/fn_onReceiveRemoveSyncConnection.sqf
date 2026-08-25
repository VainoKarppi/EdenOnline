// EOEX_fnc_onReceiveRemoveSyncConnection
params ["_fromID", "_toID", "_type"];


diag_log format ["[EdenOnline] Received RemoveSyncConnection: %1 -> %2 (%3)", _fromID, _toID, _type];


private _fromObject = objNull;
private _toObject = objNull;


// Find source object
{
    private _objectID = _x getVariable ["EOEX_var_objectID", nil];
    if (!isNil "_objectID" && {_objectID == _fromID}) exitWith { _fromObject = _x };
} forEach (all3DENEntities # 0);


// Find target object
{
    private _objectID = _x getVariable ["EOEX_var_objectID", nil];
    if (!isNil "_objectID" && {_objectID == _toID}) exitWith { _toObject = _x };
} forEach (all3DENEntities # 0);


// Validate source
if (isNull _fromObject) exitWith {
    diag_log format ["[EdenOnline] Failed to remove SyncConnection: source object not found: %1", b_fromID];
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


diag_log format ["[EdenOnline] SyncConnection removed locally: %1 (%2) -> %3 (%4)", _fromID, _fromObject, _toID, _toObject];
