// EOEX_fnc_init3DENEvents


diag_log "3DEN Online Events Initialized";

// Used to queue multiple attribute changes into a single array of changes.
if (isNil "EOEX_var_AttributeQueues") then {
    EOEX_var_AttributeQueues = createHashMap;          // object --> [ [property, value], ... ]
    EOEX_var_AttributeTimers  = createHashMap;         // object --> scriptHandle (for terminate)
};
if (isNil "EOEX_var_ApplyingRemoteChanges") then { EOEX_var_ApplyingRemoteChanges = false };
if (isNil "EOEX_var_SyncConnections") then { EOEX_var_SyncConnections = [] };
if (isNil "EOEX_var_SyncConnectionKeys") then { EOEX_var_SyncConnectionKeys = createHashMap };
if (isNil "EOEX_var_PendingObjectCreates") then { EOEX_var_PendingObjectCreates = [] };
if (isNil "EOEX_var_InFlightObjectCreates") then { EOEX_var_InFlightObjectCreates = [] };
if (isNil "EOEX_var_FailedObjectUploadBatches") then { EOEX_var_FailedObjectUploadBatches = [] };

EOEX_fnc_sendObjectBatchWithRetry = {
	params ["_batch", ["_generation", missionNamespace getVariable ["EOEX_var_SyncGeneration", 0]]];
	private _isCurrentGeneration = {
		missionNamespace getVariable ["EOEX_var_Connected", false]
		&& {missionNamespace getVariable ["EOEX_var_AcceptSyncCallbacks", false]}
		&& {_generation == (missionNamespace getVariable ["EOEX_var_SyncGeneration", 0])}
	};
	if (
		!(call _isCurrentGeneration)
	) exitWith { false };

	private _success = false;
	private _lastResult = [false, ["Unknown upload error"]];

	for "_attempt" from 0 to 2 do {
		if !(call _isCurrentGeneration) exitWith {};
		_lastResult = ["CreateObjectsBatch", [_batch], false, 10] call EOEX_fnc_callExtensionAsync;
		if (_lastResult isEqualType [] && {count _lastResult > 0} && {_lastResult select 0}) exitWith {
			_success = true;
		};
		uiSleep ([0.1, 0.25, 0.5] select _attempt);
	};

	if !(call _isCurrentGeneration) exitWith { false };

	if !(_success) then {
		EOEX_var_FailedObjectUploadBatches pushBack _batch;
		EOEX_var_LiveSyncFailed = true;
		diag_log format ["[EdenOnline] Object batch upload failed after retries: %1", _lastResult];
		["Object upload failed after retries. EdenOnline disconnected to prevent an inconsistent mission.", 1, 10] call BIS_fnc_3DENNotification;
		[1, "Live object upload failed"] spawn EOEX_fnc_disconnect;
	};

	_success
};

// * OBJECTS

// TODO add events for copy / cut / paste / undo / redo

// Runs twice, once for GROUP, once for actual UNIT, when selecting from rightside menu
// If unit is copied and pasted, this event will run ONLY once for GROUP (mssing second run for UNIT)
/*
removeAll3DENEventHandlers "OnEditableEntityAdded";
removeAll3DENEventHandlers "OnEditableEntityRemoved";
add3DENEventHandler ["OnEditableEntityAdded", {
	params ["_object"];
	
	[_object] spawn {
		params ["_object"];

		uiSleep 0.01;
		if !(_object isEqualType objNull) then {
			{
				systemChat str(_x); // UNIT
			} forEach get3DENSelected "object";

		};
	};
}];


// WORKS AS EXPECTED (always twice)
add3DENEventHandler ["OnEditableEntityRemoved", {
	params ["_object"];

	[_object] spawn {
		params ["_object"];

		uiSleep 0.01;
		if !(_object isEqualType objNull) then {
			{
				systemChat str(_x); // UNIT
			} forEach get3DENSelected "object";

		};
	};
}];

add3DENEventHandler ["OnPaste", {
	systemChat str(_this);
}];
*/
removeAll3DENEventHandlers "OnEditableEntityAdded";
add3DENEventHandler ["OnEditableEntityAdded", {
	params ["_entity"];
	if (missionNamespace getVariable ["EOEX_var_ApplyingRemoteChanges", false]) exitWith {};
	if !(missionNamespace getVariable ["EOEX_var_Connected", false]) exitWith {};
	if !(missionNamespace getVariable ["EOEX_var_AcceptSyncCallbacks", false]) exitWith {};
	
	if (EOEX_var_DEBUG) then {
		diag_log typeName _entity;
		diag_log _entity;
	};

	// Object, Trigger, System
	if (_entity isEqualType objNull) exitWith {
		if (_entity isKindOf "EmptyDetector") then {
			// TRIGGER
			[_entity] spawn EOEX_fnc_createTrigger;
		} else {
			// OBJECT AND SYSTEM
			private _id = _entity getVariable ["EOEX_var_objectID", ""];
			if (_id != "") exitWith {};

			if (EOEX_var_DEBUG) then { diag_log "NEW OBJECT CREATED" };

			_entity setVariable ["EOEX_var_createPending", true];
			// OnEditableEntityAdded is already the uniqueness boundary. pushBackUnique
			// would scan the growing array and turn large pastes into O(n^2) work.
			EOEX_var_PendingObjectCreates pushBack _entity;
			if (
				isNil "EOEX_var_ObjectCreateFlushHandle"
				|| {scriptDone EOEX_var_ObjectCreateFlushHandle}
			) then {
				EOEX_var_ObjectCreateFlushHandle = [] spawn {
					private _generation = missionNamespace getVariable ["EOEX_var_SyncGeneration", 0];
					// Gather all entities emitted by one paste/composition operation.
					uiSleep 0.03;

					while {
						missionNamespace getVariable ["EOEX_var_Connected", false]
						&& {missionNamespace getVariable ["EOEX_var_AcceptSyncCallbacks", false]}
						&& {_generation == (missionNamespace getVariable ["EOEX_var_SyncGeneration", 0])}
						&& {count EOEX_var_PendingObjectCreates > 0}
					} do {
						private _pendingObjects = +EOEX_var_PendingObjectCreates;
						EOEX_var_PendingObjectCreates = [];
						EOEX_var_InFlightObjectCreates = +_pendingObjects;

						private _objectBatch = [];
						private _batchCharacters = 0;
						private _uploadFailed = false;
						{
							if (_uploadFailed) then { continue };
							private _object = _x;
							if (isNull _object) then { continue };
							if ((_object getVariable ["EOEX_var_objectID", ""]) != "") then { continue };

							private _objectId = _object call EOEX_fnc_getId;
							private _entry = [_objectId, _object get3DENAttributes ""];
							_object setVariable ["EOEX_var_createPending", nil];
							private _entryCharacters = count str _entry;

							// One nested callExtension argument may be large, but keeping a
							// conservative payload ceiling avoids oversized TCP messages.
							if (
								_objectBatch isNotEqualTo []
								&& {count _objectBatch >= 256 || {_batchCharacters + _entryCharacters > 4000000}}
							) then {
								private _sent = [_objectBatch, _generation] call EOEX_fnc_sendObjectBatchWithRetry;
								if !(_sent) then { _uploadFailed = true };
								_objectBatch = [];
								_batchCharacters = 0;
							};

							_objectBatch pushBack _entry;
							_batchCharacters = _batchCharacters + _entryCharacters;
						} forEach _pendingObjects;

						if (!_uploadFailed && {_objectBatch isNotEqualTo []}) then {
							private _sent = [_objectBatch, _generation] call EOEX_fnc_sendObjectBatchWithRetry;
							if !(_sent) then { _uploadFailed = true };
						};

						if (_uploadFailed) then {
							{ if (!isNull _x) then { _x setVariable ["EOEX_var_createPending", nil] } } forEach _pendingObjects;
						};
						EOEX_var_InFlightObjectCreates = [];

						uiSleep 0;
					};
				};
			};
		}
	};

	// Group
	if (_entity isEqualType grpNull) exitWith {
		if (EOEX_var_DEBUG) then {
			diag_log _entity;
			diag_log "GROUP CREATED";
		};
	};

	// Marker
	if (_entity isEqualType "") exitWith {
		if (EOEX_var_DEBUG) then {
			diag_log _entity;
			diag_log "MARKER CREATED";
		};

		[_entity] spawn EOEX_fnc_createMarker
	};

	// Waypoint
	if (_entity isEqualType []) exitWith {
		if (EOEX_var_DEBUG) then {
			diag_log _entity;
			diag_log "WAYPOINT CREATED";
		};
	};

	// LAYER OR COMMENT
	if (_entity isEqualType 0) exitWith {
		// TODO Comment runs: updateObjectAttributes for some reason?
		if (EOEX_var_DEBUG) then {
			diag_log _entity;
			diag_log "LAYER OR COMMENT CREATED";
		};
	};


}];

add3DENEventHandler ["OnEditableEntityRemoved", {
	params ["_entity"];
	if (missionNamespace getVariable ["EOEX_var_ApplyingRemoteChanges", false]) exitWith {};

	// FIX UNTIL THIS GETS FIXED BY BI (single object)
	if (_entity isEqualType grpNull) exitWith {
		{
			[_x] call EOEX_fnc_deleteObject;
		} forEach get3DENSelected "object";
	};

	// OBJECT
	if (_entity isEqualType objNull) exitWith {
		[_entity] call EOEX_fnc_deleteObject;
	};
}];


add3DENEventHandler ["OnEntityAttributeChanged", {
	if (missionNamespace getVariable ["EOEX_var_ApplyingRemoteChanges", false]) exitWith {};
	_this params ["_entity"];
	if (_entity isEqualType objNull && {_entity getVariable ["EOEX_var_createPending", false]}) exitWith {};
	_this spawn EOEX_fnc_updateObjectAttributes;
}];

// * CONNECTIONS
add3DENEventHandler ["OnConnectingEnd", {
    params ["_class", "_from", "_to"];
    if (missionNamespace getVariable ["EOEX_var_ApplyingRemoteChanges", false]) exitWith {};

    if !(isNil "_to") exitWith {
        if (EOEX_var_DEBUG) then { diag_log "[EdenOnline] Dispatching CREATE connection." };

        [_class, _from, _to] spawn EOEX_fnc_createSyncConnection;
    };

    if (EOEX_var_DEBUG) then { diag_log "[EdenOnline] Dispatching REMOVE connection." };

    [_class, _from] spawn EOEX_fnc_removeSyncConnection;
}];


// * MISSION SETTINGS
remove3DENEventHandler ["OnMissionAttributeChanged", uiNamespace getVariable ["EOEX_var_OnMissionAttributeChangedId", -1]];

if (missionNamespace getVariable ["EOEX_var_syncMissionAttributes", false]) then {
	
	private _id = add3DENEventHandler ["OnMissionAttributeChanged", {
		params ["_section", "_property"];

		private _value = _section get3DENMissionAttribute _property;
		private _key = [_section, _property];

		private _skipValue = EOEX_var_SkipAttributeChange getOrDefault [_key, nil];

		// Prevent echo/feedback loop
		if (!isNil "_skipValue" && {_skipValue isEqualTo _value}) then {
			EOEX_var_SkipAttributeChange deleteAt _key;
		} else {
			[_section, _property, _value] call EOEX_fnc_updateMissionAttributes;
		};
	}];

	uiNamespace setVariable ["EOEX_var_OnMissionAttributeChangedId", _id];
};
