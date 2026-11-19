// EOEX_fnc_initExtension
if !(isNil "EOEX_var_extensionResponses") exitWith { true }; // Extension already initialized

diag_log "Initializing EdenOnline C# Extension";

// Init variables
EOEX_var_extensionName = "EdenOnline";
EOEX_var_eventsReady = false;
EOEX_var_syncMissionAttributes = true;

EOEX_var_DEBUG = false;
EOEX_var_blackListLogging = ["CameraUpdate", "SetInitialMissionAttributes", "UpdateObjectDrag"];
uiNamespace setVariable ["EOEX_var_cameraDrawUpdate", 0.2];
uiNamespace setVariable ["EOEX_var_cameraSendHeartbeat", 5]; // Max seconds between CameraUpdate sends even when the camera is idle


private _result = EOEX_var_extensionName callExtension "version";
if (_result == "") exitWith { diag_log "EdenOnline Initialization failed: Extension not found."; false }; // Extension not found. Already logged to .RPT

private _return = [];
if (_result isEqualType []) then { // Params used
	_return = (parseSimpleArray _result) select 0;
} else {
	_return = parseSimpleArray _result;
};

private _data = (_return select 1) select 0;

if (_return select 0 == "ERROR") exitWith { diag_log format ["ERROR: %1", _data]; false };

EOEX_var_DragUpdateSpeed = 0.1;

EOEX_var_extensionVersion = _data;

EOEX_var_extensionResponses = createHashMap;
EOEX_var_extensionRequests = createHashMap;

EOEX_var_SkipAttributeChanges = createHashMap;


EOEX_var_OtherClients = createHashMap;

// id -> [entity, username, mode, position, timestamp] for entities another client is editing or dragging.
EOEX_var_Locks = createHashMap;
EOEX_var_LocksHandler = nil;

// The holder renews an "attributes" lock every LockHeartbeat seconds; it is dropped when a renewal
// does not arrive within LockTimeout seconds (see fn_enforceLocks and fn_watchAttributesEdit).
EOEX_var_LockHeartbeat = 2;
EOEX_var_LockTimeout = 6;

EOEX_var_PastedObjects = [];
EOEX_var_RemovedEntities = [];
EOEX_var_Objects = createHashMap;
EOEX_var_Markers = createHashMap;
EOEX_var_Comments = createHashMap;
EOEX_var_Waypoints = createHashMap;
EOEX_var_Layers = createHashMap;


missionNamespace setVariable ["EOEX_var_MoveActive", false];
missionNamespace setVariable ["EOEX_var_MoveLastUpdate", 0];
missionNamespace setVariable ["EOEX_var_MoveId", ""];
missionNamespace setVariable ["EOEX_var_MoveIds", []];
missionNamespace setVariable ["EOEX_var_MoveEntities", []];
missionNamespace setVariable ["EOEX_var_MoveReferencePosition", [0, 0, 0]];
missionNamespace setVariable ["EOEX_var_RemoteMoveIds", []];
missionNamespace setVariable ["EOEX_var_RemoteMoveIgnoreIds", []];
missionNamespace setVariable ["EOEX_var_RemoteMoveIgnoreUntil", 0];
missionNamespace setVariable ["EOEX_var_MoveIgnoreIncomingIds", []];
missionNamespace setVariable ["EOEX_var_MoveIgnoreIncomingUntil", 0];

EOEX_var_IsHost = false;

diag_log formatText ["VERSION: %1",_data];

call EOEX_fnc_initExtensionEvents;

// Register the EachFrame interpolation handler
[] call EOEX_fnc_moveInterpolation;


true
