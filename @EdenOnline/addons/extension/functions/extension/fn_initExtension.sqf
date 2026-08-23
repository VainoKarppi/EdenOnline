// EOEX_fnc_initExtension
if !(isNil "EOEX_var_extensionRequests") exitWith { true }; // Extension already initialized


diag_log "Initializing EdenOnline C# Extension";

// Init variables
EOEX_var_extensionName = "EdenOnline";
EOEX_var_eventsReady = false;
EOEX_var_syncMissionAttributes = true;

EOEX_var_DEBUG = true;
uiNamespace setVariable ["EOEX_var_cameraDrawUpdate", 0.2];


private _result = EOEX_var_extensionName callExtension "version";
if (_result == "") exitWith { false }; // Extension not found. Already logged to .RPT

private _return = [];
if (_result isEqualType []) then { // Params used
	_return = (parseSimpleArray _result) select 0;
} else {
	_return = parseSimpleArray _result;
};

private _data = (_return select 1) select 0;

if (_return select 0 == "ERROR") exitWith { diag_log format ["ERROR: %1", _data]; false };

EOEX_var_extensionVersion = _data;

EOEX_var_extensionResponses = createHashMap;
EOEX_var_extensionRequests = createHashMap;
EOEX_var_Objects = createHashMap;
EOEX_var_OtherClients = createHashMap;
EOEX_var_SkipAttributeChange = createHashMap;
EOEX_var_ApplyingRemoteChanges = false;
EOEX_var_SyncConnections = [];
EOEX_var_SyncConnectionKeys = createHashMap;

EOEX_var_IsHost = false;

diag_log formatText ["VERSION: %1",_data];

call EOEX_fnc_initExtensionEvents;


true
