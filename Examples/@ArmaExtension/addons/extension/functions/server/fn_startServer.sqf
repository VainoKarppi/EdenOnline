

// TODO make sure server is not already running && !connected

// TODO SEND INITIAL OBJECTS TO SERVER WHEN STARTING SERVER

diag_log "STARTING SERVER";

EXT_var_expectedObjectSyncCount = -1;

_port = 2302;
_modHashes = (getLoadedModsInfo select {_x#6 != ""})  apply {_x#6};
_gameVersion = format ["%1.%2",(productVersion#2)/100 toFixed 2,(productVersion#3)];
_serverPassword = "";

_id = ["StartServer",[profileNameSteam, _port, worldName, _gameVersion, _modHashes, _serverPassword]] call EXT_fnc_callExtensionAsync;

missionNamespace setVariable ["EXT_var_clientID",_id];

[("CONNECTED TO SERVER WITH ID: " + str(_id)), 0,5] call BIS_fnc_3DENNotification;