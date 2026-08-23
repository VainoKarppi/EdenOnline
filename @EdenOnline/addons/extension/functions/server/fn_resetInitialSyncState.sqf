// EOEX_fnc_resetInitialSyncState
params [
    ["_removeSynchronizedObjects", false, [false]],
    ["_acceptCallbacks", true, [false]]
];

// Invalidate and stop any uploader from the previous connection generation.
EOEX_var_SyncGeneration = (missionNamespace getVariable ["EOEX_var_SyncGeneration", 0]) + 1;
if !(isNil "EOEX_var_ObjectCreateFlushHandle") then {
    if !(scriptDone EOEX_var_ObjectCreateFlushHandle) then {
        terminate EOEX_var_ObjectCreateFlushHandle;
    };
};
missionNamespace setVariable ["EOEX_var_ObjectCreateFlushHandle", nil];

// A failed remote join starts from an empty editor, so remove every entity
// created by that synchronization generation before returning control.
if (_removeSynchronizedObjects) then {
    private _synchronizedObjects = (keys EOEX_var_Objects) apply {
        EOEX_var_Objects getOrDefault [_x, objNull]
    };
    _synchronizedObjects = _synchronizedObjects select { !isNull _x };
    if (_synchronizedObjects isNotEqualTo []) then {
        EOEX_var_ApplyingRemoteChanges = true;
        delete3DENEntities _synchronizedObjects;
    };
};

EOEX_var_AcceptSyncCallbacks = _acceptCallbacks;
EOEX_var_expectedObjectSyncCount = -1;
EOEX_var_expectedConnectionSyncCount = -1;
EOEX_var_ObjectSyncProcessedCount = 0;
EOEX_var_ObjectSyncFailedCount = 0;
EOEX_var_ObjectSyncQueue = [];
EOEX_var_ObjectSyncQueueOffset = 0;
EOEX_var_ConnectionSyncProcessedCount = 0;
EOEX_var_ConnectionSyncFailedCount = 0;
EOEX_var_ConnectionSyncQueue = [];
EOEX_var_ConnectionSyncQueueOffset = 0;
EOEX_var_PendingObjectUpdates = createHashMap;
EOEX_var_PendingObjectRemovals = createHashMap;
EOEX_var_PendingConnectionRemovals = createHashMap;
EOEX_var_LiveObjectIds = createHashMap;
EOEX_var_LiveSyncFailed = false;
private _pendingCreateObjects =
    +(missionNamespace getVariable ["EOEX_var_PendingObjectCreates", []])
    + (missionNamespace getVariable ["EOEX_var_InFlightObjectCreates", []]);
{
    if (_x isEqualType objNull && {!isNull _x}) then {
        _x setVariable ["EOEX_var_createPending", nil];
    };
} forEach _pendingCreateObjects;
EOEX_var_PendingObjectCreates = [];
EOEX_var_InFlightObjectCreates = [];
if (_acceptCallbacks) then { EOEX_var_FailedObjectUploadBatches = [] };
EOEX_var_ObjectSyncApplying = false;
EOEX_var_ApplyingRemoteChanges = false;
EOEX_var_ObjectSyncReleaseFrame = -1;
EOEX_var_Objects = createHashMap;
EOEX_var_SyncConnections = [];
EOEX_var_SyncConnectionKeys = createHashMap;
