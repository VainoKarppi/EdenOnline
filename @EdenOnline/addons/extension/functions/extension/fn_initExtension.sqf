// EOEX_fnc_initExtension
if !(isNil "EOEX_var_extensionRequests") exitWith { true }; // Extension already initialized


diag_log "Initializing EdenOnline C# Extension";

// Init variables
EOEX_var_extensionName = "EdenOnline";
EOEX_var_eventsReady = false;
EOEX_var_syncMissionAttributes = true;

// High-volume RPT logging is opt-in because it multiplies the cost of large
// editor operations.
EOEX_var_DEBUG = false;
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
EOEX_var_extensionRequestId = 0;
EOEX_var_Objects = createHashMap;
EOEX_var_OtherClients = createHashMap;
EOEX_var_SkipAttributeChange = createHashMap;
EOEX_var_ApplyingRemoteChanges = false;
EOEX_var_AcceptSyncCallbacks = false;
EOEX_var_ObjectSyncApplying = false;
EOEX_var_ObjectSyncQueue = [];
EOEX_var_ObjectSyncQueueOffset = 0;
EOEX_var_ObjectSyncProcessedCount = 0;
EOEX_var_ObjectSyncFailedCount = 0;
EOEX_var_ObjectSyncReleaseFrame = -1;
EOEX_var_ObjectSyncFrameBudget = 0.006;
EOEX_var_ObjectSyncFrameLimit = 96;
EOEX_var_expectedConnectionSyncCount = -1;
EOEX_var_ConnectionSyncProcessedCount = 0;
EOEX_var_ConnectionSyncFailedCount = 0;
EOEX_var_ConnectionSyncQueue = [];
EOEX_var_ConnectionSyncQueueOffset = 0;
EOEX_var_PendingObjectUpdates = createHashMap;
EOEX_var_PendingObjectRemovals = createHashMap;
EOEX_var_PendingConnectionRemovals = createHashMap;
EOEX_var_LiveObjectIds = createHashMap;
EOEX_var_LiveSyncFailed = false;
EOEX_var_PendingObjectCreates = [];
EOEX_var_FailedObjectUploadBatches = [];
EOEX_var_SyncConnections = [];
EOEX_var_SyncConnectionKeys = createHashMap;

EOEX_var_IsHost = false;

diag_log formatText ["VERSION: %1",_data];

call EOEX_fnc_initExtensionEvents;


true
