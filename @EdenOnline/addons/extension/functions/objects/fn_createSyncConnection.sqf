// EOEX_fnc_createSyncConnection
params ["_class", "_from", "_to"];

if !(missionNamespace getVariable ["EOEX_var_Connected", false]) exitWith {};

if (isNil "_class") exitWith {};
if (isNil "_from") exitWith {};
if (isNil "_to") exitWith {};

private _type = _class;

if (_type == "") exitWith {
    diag_log "[EdenOnline] Cannot create connection: connection type is empty.";
};

// Make sure the connection list exists.
if (isNil "EOEX_var_SyncConnections") then {
    EOEX_var_SyncConnections = [];
};

// For "Sync", sources are stored in the first connection category.
private _sources = _from select 0;

if !(_sources isEqualType []) exitWith {
    diag_log format ["[EdenOnline] Cannot create connection: invalid source array: %1", _sources];
};

private _toID = _to getVariable ["EOEX_var_objectID", nil];

if (isNil "_toID") exitWith {
    diag_log format ["[EdenOnline] Cannot create connection: target has no object ID: %1", _to];
};

{
    private _fromObject = _x;

    private _fromID = _fromObject getVariable ["EOEX_var_objectID", nil];

    if (isNil "_fromID") then {
        diag_log format ["[EdenOnline] Cannot create connection: source has no object ID: %1", _fromObject];
    } else {
        private _connection = [_fromID, _toID, _type];

        private _connectionKey = str _connection;

        // Prevent duplicate local registrations with an O(1) lookup.
        if !(EOEX_var_SyncConnectionKeys getOrDefault [_connectionKey, false]) then {
            ["CreateSyncConnection", [_fromID, _toID, _type]] call EOEX_fnc_callExtensionAsync;

            EOEX_var_SyncConnections pushBack _connection;
            EOEX_var_SyncConnectionKeys set [_connectionKey, true];

            if (EOEX_var_DEBUG) then {
                diag_log format ["[EdenOnline] Connection created: %1 -> %2 (%3)", _fromID, _toID, _type];
            };

        } else {
            if (EOEX_var_DEBUG) then {
                diag_log format ["[EdenOnline] Connection already registered: %1 -> %2 (%3)", _fromID, _toID, _type];
            };
        };
    };

} forEach _sources;
