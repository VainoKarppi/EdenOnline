
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

private _modHashes = (getLoadedModsInfo select {_x#6 != ""})  apply {_x#6};
_modHashes sort true;

private _gameVersion = format ["%1.%2",(productVersion#2)/100 toFixed 2,(productVersion#3)];
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


// Send current world edits to server
{
    private _attributes = (_x get3DENAttributes "");
    private _id = _x call EOEX_fnc_getId;

    ["CreateObject", [_id, _attributes]] call EOEX_fnc_callExtensionAsync;
} forEach (all3DENEntities # 0);

uiSleep 0.1;


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
[] spawn EOEX_fnc_drawCameras;
[] spawn EOEX_fnc_showPlayersDialog;

[("CONNECTED TO SERVER WITH ID: " + str(_id)), 0,5] call BIS_fnc_3DENNotification;

missionNamespace setVariable ["EOEX_var_Connected", true];
missionNamespace setVariable ["EOEX_var_IsHost", true];

// Disable ability to preview the mission
[false] call EOEX_fnc_togglePlayButtons;

endLoadingScreen;

_return
