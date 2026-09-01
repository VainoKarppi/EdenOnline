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

EOEX_var_expectedObjectSyncCount = -1;

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
    endLoadingScreen;
    diag_log _return;
	[(format ["%1", _return#1]), 1, 5] call BIS_fnc_3DENNotification;
};

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


private _allObjects = all3DENEntities;

// Send current OBJECTS to server
{
    private _attributes = (_x get3DENAttributes "");
    private _id = _x call EOEX_fnc_getId;

    ["CreateObject", [_id, "Object", _attributes]] call EOEX_fnc_callExtensionAsync;
} forEach _allObjects#0;


uiSleep 0.1;



// Send initial synced items list to server
private _sentConnections = createHashMap;

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

        ["CreateSyncConnection", [_id, _toID, _connectionType]] call EOEX_fnc_callExtensionAsync;

    } forEach _connections;

} forEach _allObjects;


private _timeoutSeconds = 30;
private _startTime = diag_tickTime;

while {EOEX_var_expectedObjectSyncCount == -1 || (count (all3DENEntities # 0)) < EOEX_var_expectedObjectSyncCount} do {

	// Timeout check
    if ((diag_tickTime - _startTime) > _timeoutSeconds) exitWith {
        ["Server sync timed out!", 1, 5] call BIS_fnc_3DENNotification;
        missionNamespace setVariable ["EOEX_var_Connected", false];
        endLoadingScreen;
    };

    if (EOEX_var_expectedObjectSyncCount > 0) then {
        private _spawned = count (all3DENEntities # 0);
        private _expected = EOEX_var_expectedObjectSyncCount;

        private _progress = _spawned / _expected;

        // Clamp just in case
        if (_progress > 1) then { _progress = 1; };

        progressLoadingScreen _progress;
    };

    uiSleep 0.01;
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
