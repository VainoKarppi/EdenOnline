// EOEX_fnc_connect

params [["_host","127.0.0.1",[""]], ["_port",2302,[0]], ["_password","",[""]]];

// TODO make sure server is not already running && !connected

if (missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {
	["YOU ARE ALREADY CONNECTED", 1, 5] call BIS_fnc_3DENNotification;
};


private _continue = true;
if ((all3DENEntities) isNotEqualTo [[],[],[],[],[],[],[],[-999]]) then {
    uiNamespace setVariable ["EOEX_var_ButtonConfirmed", nil];

    [
        "<t align='center'>Connecting to a server will delete your current world.</t><br/><br/><t align='center' font='PuristaMedium'>Do you want to continue?</t>",
        "Warning",
        [
            "Yes",
            { uiNamespace setVariable ["EOEX_var_ButtonConfirmed", true] }
        ],
        [
            "No",
            { uiNamespace setVariable ["EOEX_var_ButtonConfirmed", false] }
        ],
        "\A3\ui_f\data\igui\cfg\simpleTasks\types\danger_ca.paa",
        findDisplay 313
    ] call BIS_fnc_3DENShowMessage;

    waitUntil { !isNil { uiNamespace getVariable "EOEX_var_ButtonConfirmed" } };

    _continue = uiNamespace getVariable ["EOEX_var_ButtonConfirmed", false];
    if !(_continue) exitWith {};
    
    // OK was pressed, so delete all progress and entities
    // ["_objects", "_groups", "_triggers", "_systems", "_waypoints", "_markers", "_layers", "_comments"];
    for "_i" from 0 to 7 do {
        delete3DENEntities (all3DENEntities select _i);
    };
};

if !(_continue) exitWith {};


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

uiSleep 0.5;

private _return = ["Connect",[_host, _port, profileNameSteam, worldName, _gameVersion, _modHashes, _password], false, 180] call EOEX_fnc_callExtensionAsync;

diag_log _return;

if !(_return#0) exitWith {
    [true, false] call EOEX_fnc_resetInitialSyncState;
    endLoadingScreen;
	[(format ["%1", _return#1#0]), 1, 5] call BIS_fnc_3DENNotification;
};

private _id = ((_return select 1) select 0);

//private _otherClients = ((_return select 1) select 0) select 1;
//EOEX_var_OtherClients = createHashMapFromArray _otherClients;

missionNamespace setVariable ["EOEX_var_clientID",_id];

// Wait until objects have been syncronised

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
    [true, false] call EOEX_fnc_resetInitialSyncState;
    missionNamespace setVariable ["EOEX_var_Connected", false];
    endLoadingScreen;
    [format ["Sync failed for %1 objects and %2 connections.", _failedObjects, _failedConnections], 1, 8] call BIS_fnc_3DENNotification;
    ["Disconnect", [], false, 10] call EOEX_fnc_callExtensionAsync;
    [false, ["Object synchronization did not complete."]]
};

// TODO send extensions request mission attributes



// (findDisplay 313 displayCtrl 1023) ctrlEnable true

call EOEX_fnc_init3DENEvents;
[] spawn EOEX_fnc_drawCameras;
[] spawn EOEX_fnc_showPlayersDialog;

[("CONNECTED TO SERVER WITH ID: " + str(_id)), 0,5] call BIS_fnc_3DENNotification;

missionNamespace setVariable ["EOEX_var_Connected", true];


// disable ability to preview the mission
[false] call EOEX_fnc_togglePlayButtons;

endLoadingScreen;

_return
