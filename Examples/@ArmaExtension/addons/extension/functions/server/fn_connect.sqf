
params [["_host","127.0.0.1",[""]], ["_port",2302,[0]], ["_password","",[""]]];

// TODO make sure server is not already running && !connected

if (missionNamespace getVariable ["EXT_var_Connected",false]) exitWith {
	["YOU ARE ALREADY CONNECTED", 1, 5] call BIS_fnc_3DENNotification;
};

if !((all3DENEntities) isEqualto [[],[],[],[],[],[],[],[-999]]) exitWith {
	["World must be empty first!", 1, 5] call BIS_fnc_3DENNotification;
};

EXT_var_expectedObjectSyncCount = -1;

private _modHashes = (getLoadedModsInfo select {_x#6 != ""})  apply {_x#6};
private _gameVersion = format ["%1.%2",(productVersion#2)/100 toFixed 2,(productVersion#3)];
private _password = "";

startLoadingScreen ["Starting server..."];

uiSleep 0.5;

private _return = ["Connect",[_host, _port, profileNameSteam, worldName, _gameVersion, _modHashes, _password]] call EXT_fnc_callExtensionAsync;


diag_log _return;

if !(_return#0) exitWith {
	[(format ["%1", _return#1]), 1, 5] call BIS_fnc_3DENNotification;
	endLoadingScreen;
};

private _id = ((_return select 1) select 0) select 0;
private _otherClients = ((_return select 1) select 0) select 1;

diag_log format ["%1", _otherClients];

EXT_var_OtherClients = createHashMapFromArray _otherClients;

missionNamespace setVariable ["EXT_var_clientID",_id];

// Start object synchronize

private _timeoutSeconds = 30;
private _startTime = diag_tickTime;

while {EXT_var_expectedObjectSyncCount == -1 || (count (all3DENEntities # 0)) < EXT_var_expectedObjectSyncCount} do {

	// Timeout check
    if ((diag_tickTime - _startTime) > _timeoutSeconds) exitWith {
        ["Server sync timed out!", 1, 5] call BIS_fnc_3DENNotification;
        missionNamespace setVariable ["EXT_var_Connected", false];
        endLoadingScreen;
    };

    if (EXT_var_expectedObjectSyncCount > 0) then {
        private _spawned = count (all3DENEntities # 0);
        private _expected = EXT_var_expectedObjectSyncCount;

        private _progress = _spawned / _expected;

        // Clamp just in case
        if (_progress > 1) then { _progress = 1; };

        progressLoadingScreen _progress;
    };

    uiSleep 0.01;
};

missionNamespace setVariable ["EXT_var_Connected",true];

call EXT_fnc_init3DENEvents;

[("CONNECTED TO SERVER WITH ID: " + str(_id)), 0,5] call BIS_fnc_3DENNotification;

endLoadingScreen;

_return