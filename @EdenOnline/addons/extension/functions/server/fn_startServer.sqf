// EOEX_fnc_startServer

params [["_port",2302,[0]], ["_password","",[""]]];


// TODO SEND INITIAL OBJECTS TO SERVER WHEN STARTING SERVER


if (missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {
	["YOU ARE ALREADY CONNECTED", 1, 5] call BIS_fnc_3DENNotification;
};

/*
if !((all3DENEntities) isEqualto [[],[],[],[],[],[],[],[-999]]) exitWith {
	["World must be empty first!", 1, 5] call BIS_fnc_3DENNotification;
};
*/

// Reset the synchronization generation before opening the socket. Count
// callbacks only publish expectations; they must never clear live events that
// raced with the snapshot.
[false, true] call EOEX_fnc_resetInitialSyncState;

//private _modHashes = (getLoadedModsInfo select {_x#6 != ""})  apply {_x#6};
private _modHashes = [];
_modHashes sort true;

private _gameVersion = format ["%1",(productVersion#2)/100 toFixed 2];
private _password = "";

startLoadingScreen ["Starting server..."];

uiSleep 0.1;

private _return = ["StartServer",[_port, profileNameSteam, worldName, _gameVersion, _modHashes, _password], false, 5] call EOEX_fnc_callExtensionAsync;


// TODO Verify the client is is 2 and especially NOT -1. Throw error
if !(_return#0) exitWith {
    [false, false] call EOEX_fnc_resetInitialSyncState;
    endLoadingScreen;
    diag_log _return;
	[(format ["%1", _return#1]), 1, 5] call BIS_fnc_3DENNotification;
};

diag_log _return;
private _id = ((_return select 1) select 0);

//EOEX_var_OtherClients = createHashMapFromArray _otherClients;

missionNamespace setVariable ["EOEX_var_clientID",_id];




// Send mission attributes if enabled
if (missionNamespace getVariable ["EOEX_var_syncMissionAttributes", false]) then {
    private _attributeList = "true" configClasses (configFile >> "Cfg3DEN" >> "Mission") apply {
        private _sectionCfg = _x;
        private _attributes = [];
    
        {
            _attributes append (
                "true" configClasses (_x >> "Attributes") apply { getText (_x >> "data") }
            );
        } forEach ("true" configClasses (_sectionCfg >> "AttributeCategories"));

        [configName _sectionCfg, _attributes]
    };

    private _attributes = [];
    {
        private _section = _x#0;
        private _properties = _x#1;
        {
            private _property = _x;
            if (_property != "") then {
                private _value = (_section get3DENMissionAttribute _property);
                if (isNil "_value") then {
                    _attributes pushBack [_section, _property, nil];
                } else {
                    _attributes pushBack [_section, _property, _value];
                }; 
            };
        } forEach _properties;
    } forEach _attributeList;

    ["SetInitialMissionAttributes", [_attributes]] call EOEX_fnc_callExtensionAsync;
};


private _allObjects = (all3DENEntities # 0);
// Send current world edits to server
private _objectBatch = [];
private _objectBatchCharacters = 0;
private _uploadedObjectCount = 0;
private _totalObjectCount = count _allObjects;
{
    private _attributes = (_x get3DENAttributes "");
    private _id = _x call EOEX_fnc_getId;
    private _entry = [_id, _attributes];
    private _entryCharacters = count str _entry;

    if (
        _objectBatch isNotEqualTo []
        && {count _objectBatch >= 256 || {_objectBatchCharacters + _entryCharacters > 4000000}}
    ) then {
        ["CreateObjectsBatch", [_objectBatch], false, 30] call EOEX_fnc_callExtensionAsync;
        _objectBatch = [];
        _objectBatchCharacters = 0;

        if (_totalObjectCount > 0) then {
            progressLoadingScreen (0.1 + (0.6 * (_uploadedObjectCount / _totalObjectCount)));
        };
    };

    _objectBatch pushBack _entry;
    _objectBatchCharacters = _objectBatchCharacters + _entryCharacters;
    _uploadedObjectCount = _uploadedObjectCount + 1;
} forEach _allObjects;

if (_objectBatch isNotEqualTo []) then {
    ["CreateObjectsBatch", [_objectBatch], false, 30] call EOEX_fnc_callExtensionAsync;
};

progressLoadingScreen 0.7;
uiSleep 0.1;



// Send initial synced items list to server
private _sentConnections = createHashMap;
private _connectionBatch = [];

{
    private _object = _x;
    private _connections = get3DENConnections _object;

    if (isNil "_connections") then { continue };

    private _id = _object call EOEX_fnc_getId;

    if (isNil "_id") then { continue};

    {
        private _connectionType = _x#0;
        private _targetObject = _x#1;

        // TODO FIX GROUPS
        if (_connectionType != "Sync") then { continue };

        private _toID = _targetObject call EOEX_fnc_getId;

        if (isNil "_toID") then { continue };

        // Create direction-independent key
        private _ids = [_id, _toID];
        _ids sort true;

        private _key = format ["%1|%2|%3", _ids#0, _ids#1, _connectionType];

        // Already sent this connection
        if (_sentConnections getOrDefault [_key, false]) then { continue };

        _sentConnections set [_key, true];

        _connectionBatch pushBack [_id, _toID, _connectionType];
        private _localConnection = [_id, _toID, _connectionType];
        EOEX_var_SyncConnections pushBack _localConnection;
        EOEX_var_SyncConnectionKeys set [str _localConnection, true];

        if ((count _connectionBatch) >= 512) then {
            ["CreateSyncConnectionsBatch", [_connectionBatch], false, 30] call EOEX_fnc_callExtensionAsync;
            _connectionBatch = [];
        };

    } forEach _connections;

} forEach _allObjects;

if (_connectionBatch isNotEqualTo []) then {
    ["CreateSyncConnectionsBatch", [_connectionBatch], false, 30] call EOEX_fnc_callExtensionAsync;
};

progressLoadingScreen 0.95;

// This request is ordered after the fire-and-forget uploads on the same TCP
// connection. The server validates both counts before admitting remote joins.
private _initialSyncResult = [
    "CompleteInitialSync",
    [_totalObjectCount, count _sentConnections],
    false,
    30
] call EOEX_fnc_callExtensionAsync;

if !(_initialSyncResult select 0) exitWith {
    [false, false] call EOEX_fnc_resetInitialSyncState;
    endLoadingScreen;
    missionNamespace setVariable ["EOEX_var_Connected", false];
    [format ["Initial server upload failed: %1", _initialSyncResult select 1], 1, 8] call BIS_fnc_3DENNotification;
    ["Disconnect", [], false, 10] call EOEX_fnc_callExtensionAsync;
    _initialSyncResult
};

progressLoadingScreen 1;


private _timeoutSeconds = 60 max (EOEX_var_expectedObjectSyncCount / 100);
private _startTime = diag_tickTime;

private _syncTimedOut = false;
while {
    EOEX_var_expectedObjectSyncCount == -1
    || EOEX_var_ObjectSyncProcessedCount < EOEX_var_expectedObjectSyncCount
    || EOEX_var_expectedConnectionSyncCount == -1
    || EOEX_var_ConnectionSyncProcessedCount < EOEX_var_expectedConnectionSyncCount
    || count EOEX_var_ObjectSyncQueue > 0
    || count EOEX_var_ConnectionSyncQueue > 0
} do {

	// Timeout check
    if ((diag_tickTime - _startTime) > _timeoutSeconds) exitWith {
		_syncTimedOut = true;
        ["Server sync timed out!", 1, 5] call BIS_fnc_3DENNotification;
        missionNamespace setVariable ["EOEX_var_Connected", false];
        endLoadingScreen;
    };

    if (EOEX_var_expectedObjectSyncCount >= 0 && EOEX_var_expectedConnectionSyncCount >= 0) then {
        private _spawned = EOEX_var_ObjectSyncProcessedCount + EOEX_var_ConnectionSyncProcessedCount;
        private _expected = EOEX_var_expectedObjectSyncCount + EOEX_var_expectedConnectionSyncCount;

        private _progress = if (_expected > 0) then { _spawned / _expected } else { 1 };

        // Clamp just in case
        if (_progress > 1) then { _progress = 1; };

        progressLoadingScreen _progress;
    };

    uiSleep 0.01;
};

if (_syncTimedOut || EOEX_var_ObjectSyncFailedCount > 0 || EOEX_var_ConnectionSyncFailedCount > 0 || EOEX_var_LiveSyncFailed) exitWith {
    private _failedObjects = EOEX_var_ObjectSyncFailedCount;
    private _failedConnections = EOEX_var_ConnectionSyncFailedCount;
    [false, false] call EOEX_fnc_resetInitialSyncState;
    missionNamespace setVariable ["EOEX_var_Connected", false];
    endLoadingScreen;
    [format ["Sync failed for %1 objects and %2 connections.", _failedObjects, _failedConnections], 1, 8] call BIS_fnc_3DENNotification;
    ["Disconnect", [], false, 10] call EOEX_fnc_callExtensionAsync;
    [false, ["Object synchronization did not complete."]]
};


call EOEX_fnc_init3DENEvents;


[("CONNECTED TO SERVER WITH ID: " + str(_id)), 0,5] call BIS_fnc_3DENNotification;

missionNamespace setVariable ["EOEX_var_Connected", true];
missionNamespace setVariable ["EOEX_var_IsHost", true];

// Disable ability to preview the mission
[false] call EOEX_fnc_togglePlayButtons;

[] spawn EOEX_fnc_drawCameras;
call EOEX_fnc_showPlayersDialog;

endLoadingScreen;

_return
