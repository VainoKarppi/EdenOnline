params ["_class", "_from"];

diag_log format [
    "[EdenOnline] fn_removeSyncConnection called: class=%1 from=%2",
    _class,
    _from
];

if !(missionNamespace getVariable ["EOEX_var_Connected", false]) exitWith {
    diag_log "[EdenOnline] RemoveSyncConnection: client is not connected.";
};

if (isNil "_class") exitWith {
    diag_log "[EdenOnline] RemoveSyncConnection: class is nil.";
};

if (isNil "_from") exitWith {
    diag_log "[EdenOnline] RemoveSyncConnection: from is nil.";
};

private _type = _class;

// For Sync connections, the source objects are in the first element.
private _sources = if (count _from > 0) then {
    _from select 0
} else {
    []
};

if (_sources isEqualTo []) exitWith {
    diag_log "[EdenOnline] RemoveSyncConnection: no source objects found.";
};

private _connections = missionNamespace getVariable ["EOEX_var_SyncConnections", []];

if (_connections isEqualTo []) exitWith {
    diag_log "[EdenOnline] RemoveSyncConnection: no stored connections.";
};

private _removedConnections = [];

{
    private _sourceObject = _x;

    private _fromID = _sourceObject getVariable ["EOEX_var_objectID", nil];

    if (isNil "_fromID") then {
        diag_log format ["[EdenOnline] RemoveSyncConnection: source has no object ID: %1", _sourceObject];
    } else {

        private _matches = _connections select { count _x >= 3 && (_x select 0) isEqualTo _fromID && (_x select 2) isEqualTo _type };

        {
            private _connection = _x;

            private _toID = _connection select 1;

            diag_log format ["[EdenOnline] Removing synchronization: %1 -> %2 (%3)", _fromID, _toID, _type];

            ["RemoveSyncConnection",[_fromID, _toID, _type]] call EOEX_fnc_callExtensionAsync;

            _removedConnections pushBack _connection;

        } forEach _matches;
    };

} forEach _sources;

// Remove the connections from the local registry.
{
    private _index = _connections find _x;

    if (_index != -1) then {
        _connections deleteAt _index;
    };
} forEach _removedConnections;

missionNamespace setVariable ["EOEX_var_SyncConnections", _connections];

diag_log format ["[EdenOnline] RemoveSyncConnection complete. Removed %1 connection(s).", count _removedConnections
];