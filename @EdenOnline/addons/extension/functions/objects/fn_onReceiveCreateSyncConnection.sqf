params ["_fromID", "_toID", "_type"];


diag_log format ["[EdenOnline] Received SyncConnection: %1 -> %2 (%3)", _fromID, _toID, _type];


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


diag_log format ["[EdenOnline] SyncConnection added locally: %1 (%2) -> %3 (%4)", _fromID, _fromObject, _toID, _toObject];