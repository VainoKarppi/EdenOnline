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

EOEX_fnc_readObjectDragTransform = {
	params ["_entity"];
	private _position = (_entity get3DENAttribute "Position") param [0, getPosATL _entity];
	private _rotation = (_entity get3DENAttribute "Rotation") param [0, [getDir _entity, 0, 0]];
	[_position, _rotation]
};

EOEX_fnc_finishLocalObjectDrags = {
	params [["_onlyObjectID", "", [""]]];
	private _localDrags = missionNamespace getVariable ["EOEX_var_LocalObjectDrags", createHashMap];
	private _objectIDs = if (_onlyObjectID == "") then { keys _localDrags } else { [_onlyObjectID] };

	{
		private _objectID = _x;
		private _state = _localDrags getOrDefault [_objectID, createHashMap];
		if (count _state == 0) then { continue };

		// Remove the local state before starting the reliable END call. Any
		// further OnEntityDragged event or delayed local UDP task is now stale.
		_localDrags deleteAt _objectID;
		private _entity = _state getOrDefault ["object", objNull];
		private _dragID = _state getOrDefault ["dragID", ""];
		private _sequence = (_state getOrDefault ["sequence", 0]) + 1;
		if (isNull _entity || _dragID == "") then { continue };

		([_entity] call EOEX_fnc_readObjectDragTransform) params ["_position", "_rotation"];
		[_objectID, _dragID, _sequence, _position, _rotation] spawn {
			params ["_objectID", "_dragID", "_sequence", "_position", "_rotation"];
			private _ended = false;
			private _lastResult = [false, ["END_DRAG was not attempted"]];
			for "_attempt" from 0 to 2 do {
				_lastResult = [
					"EndObjectDrag",
					[_objectID, _dragID, _sequence, _position, _rotation],
					false,
					5
				] call EOEX_fnc_callExtensionAsync;
				if (
					_lastResult isEqualType []
					&& {count _lastResult > 1}
					&& {_lastResult select 0}
					&& {(_lastResult select 1) param [0, false]}
				) exitWith {
					_ended = true;
				};
				uiSleep ([0.1, 0.25, 0.5] select _attempt);
			};
			if !(_ended) then {
				diag_log format ["[EdenOnline] END_DRAG failed for %1/%2: %3", _objectID, _dragID, _lastResult];
				["AbortObjectDrag", [_objectID, _dragID], false, 5] call EOEX_fnc_callExtensionAsync;
				["The final object position could not be synchronized.", 1, 8] call BIS_fnc_3DENNotification;
			};
		};
	} forEach _objectIDs;
};

EOEX_fnc_beginLocalObjectDrag = {
	params ["_entity", "_objectID"];
	if (
		_objectID == ""
		|| {_objectID in EOEX_var_LocalObjectDrags}
		|| {_objectID in EOEX_var_PendingObjectDrags}
		|| {_objectID in EOEX_var_RemoteObjectDrags}
	) exitWith {};

	([_entity] call EOEX_fnc_readObjectDragTransform) params ["_basePosition", "_baseRotation"];
	private _baseAttributes = createHashMapFromArray (_entity get3DENAttributes "");
	EOEX_var_PendingObjectDrags set [_objectID, createHashMapFromArray [
		["object", _entity],
		["released", false],
		["basePosition", _basePosition],
		["baseRotation", _baseRotation],
		["baseAttributes", _baseAttributes]
	]];
	[_entity, _objectID] spawn {
		params ["_entity", "_objectID"];
		private _result = ["StartObjectDrag", [_objectID], false, 5] call EOEX_fnc_callExtensionAsync;
		private _pending = EOEX_var_PendingObjectDrags getOrDefault [_objectID, createHashMap];
		if (count _pending == 0) exitWith {};
		private _releasedBeforeStart = _pending getOrDefault ["released", false];
		private _basePosition = _pending getOrDefault ["basePosition", getPosATL _entity];
		private _baseRotation = _pending getOrDefault ["baseRotation", [getDir _entity, 0, 0]];
		private _baseAttributes = _pending getOrDefault ["baseAttributes", createHashMap];
		EOEX_var_PendingObjectDrags deleteAt _objectID;

		if !(_result isEqualType [] && {count _result > 1} && {_result select 0}) exitWith {
			[_entity, _basePosition, _baseRotation] call EOEX_fnc_applyObjectDragTransform;
			diag_log format ["[EdenOnline] START_DRAG failed for %1: %2", _objectID, _result];
			["Object dragging could not be synchronized.", 1, 8] call BIS_fnc_3DENNotification;
		};

		private _dragID = (_result select 1) param [0, ""];
		// An empty result means at least one peer rejected the reservation or
		// another drag already owns the object.
		if (_dragID == "" || {_objectID in EOEX_var_RemoteObjectDrags}) exitWith {
			[_entity, _basePosition, _baseRotation] call EOEX_fnc_applyObjectDragTransform;
		};

		private _state = createHashMapFromArray [
			["object", _entity],
			["dragID", _dragID],
			["sequence", 0],
			["lastSend", diag_tickTime - 0.1],
			["basePosition", _basePosition],
			["baseRotation", _baseRotation],
			["baseAttributes", _baseAttributes]
		];
		EOEX_var_LocalObjectDrags set [_objectID, _state];

		if (_releasedBeforeStart) then {
			[_objectID] call EOEX_fnc_finishLocalObjectDrags;
		};
	};
};

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
						private _uploadFailed = false;
						{
							if (_uploadFailed) then { continue };
							private _object = _x;
							if (isNull _object) then { continue };
							if ((_object getVariable ["EOEX_var_objectID", ""]) != "") then { continue };

							private _objectId = _object call EOEX_fnc_getId;
							private _entry = [_objectId, _object get3DENAttributes ""];
							_object setVariable ["EOEX_var_createPending", nil];
							_objectBatch pushBack _entry;
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
	_this params ["_entity", "_property"];
	if (_entity isEqualType objNull && {_entity getVariable ["EOEX_var_createPending", false]}) exitWith {};
	if (_entity isEqualType objNull && {_property isEqualType ""}) then {
		private _objectID = _entity getVariable ["EOEX_var_objectID", ""];
		private _remoteState = EOEX_var_RemoteObjectDrags getOrDefault [_objectID, createHashMap];
		if (count _remoteState > 0) exitWith {
			private _baseAttributes = _remoteState getOrDefault ["baseAttributes", createHashMap];
			if (_property in _baseAttributes) then {
				private _previousApplyingRemoteChanges = EOEX_var_ApplyingRemoteChanges;
				EOEX_var_ApplyingRemoteChanges = true;
				ignore3DENHistory {
					_entity set3DENAttribute [_property, _baseAttributes get _property];
				};
				EOEX_var_ApplyingRemoteChanges = _previousApplyingRemoteChanges;
			};
		};
		if (
			(toLower _property) in ["position", "rotation"]
			&& {
				_objectID in EOEX_var_LocalObjectDrags
					|| {_objectID in EOEX_var_PendingObjectDrags}
			}
		) exitWith {};
	};
	_this spawn EOEX_fnc_updateObjectAttributes;
}];

remove3DENEventHandler ["OnEntityDragged", missionNamespace getVariable ["EOEX_var_OnEntityDraggedId", -1]];
EOEX_var_OnEntityDraggedId = add3DENEventHandler ["OnEntityDragged", {
	params ["_entity"];
	if !(missionNamespace getVariable ["EOEX_var_Connected", false]) exitWith {};
	if !(missionNamespace getVariable ["EOEX_var_AcceptSyncCallbacks", false]) exitWith {};
	if !(_entity isEqualType objNull) exitWith {};

	private _objectID = _entity call EOEX_fnc_getId;
	if (_objectID == "") exitWith {};

	private _remoteState = EOEX_var_RemoteObjectDrags getOrDefault [_objectID, createHashMap];
	if (count _remoteState > 0) exitWith {
		// A remote drag is an edit lock. Deselecting immediately stops the local
		// transform operation; the interpolation worker restores the network pose.
		private _selectedObjects = get3DENSelected "object";
		if (_entity in _selectedObjects) then {
			set3DENSelected (_selectedObjects - [_entity]);
		};
	};

	if !(_objectID in EOEX_var_LocalObjectDrags) exitWith {
		[_entity, _objectID] call EOEX_fnc_beginLocalObjectDrag;
	};

	private _state = EOEX_var_LocalObjectDrags get _objectID;
	private _now = diag_tickTime;
	if ((_now - (_state getOrDefault ["lastSend", 0])) < 0.1) exitWith {};

	private _sequence = (_state getOrDefault ["sequence", 0]) + 1;
	([_entity] call EOEX_fnc_readObjectDragTransform) params ["_position", "_rotation"];
	_state set ["sequence", _sequence];
	_state set ["lastSend", _now];
	EOEX_var_LocalObjectDrags set [_objectID, _state];

	[_objectID, _state get "dragID", _sequence, _position, _rotation] spawn {
		params ["_objectID", "_dragID", "_sequence", "_position", "_rotation"];
		["UpdateObjectDrag", [_objectID, _dragID, _sequence, _position, _rotation], false, 1]
			call EOEX_fnc_callExtensionAsync;
	};
}];

remove3DENEventHandler ["OnSelectionChange", missionNamespace getVariable ["EOEX_var_ObjectDragSelectionId", -1]];
EOEX_var_ObjectDragSelectionId = add3DENEventHandler ["OnSelectionChange", {
	private _selectedObjects = get3DENSelected "object";
	private _lockedObjects = _selectedObjects select {
		private _objectID = _x getVariable ["EOEX_var_objectID", ""];
		_objectID in EOEX_var_RemoteObjectDrags
	};
	if (_lockedObjects isNotEqualTo []) then {
		set3DENSelected (_selectedObjects - _lockedObjects);
	};
}];

private _display3DEN = findDisplay 313;
if !(isNull _display3DEN) then {
	private _previousMouseUp = uiNamespace getVariable ["EOEX_var_ObjectDragMouseUpId", -1];
	if (_previousMouseUp >= 0) then {
		_display3DEN displayRemoveEventHandler ["MouseButtonUp", _previousMouseUp];
	};

	private _mouseUpID = _display3DEN displayAddEventHandler ["MouseButtonUp", {
		params ["_display", "_button"];
		if (_button != 0) exitWith {};

		{
			_y set ["released", true];
			EOEX_var_PendingObjectDrags set [_x, _y];
		} forEach EOEX_var_PendingObjectDrags;
		[] call EOEX_fnc_finishLocalObjectDrags;
	}];
	uiNamespace setVariable ["EOEX_var_ObjectDragMouseUpId", _mouseUpID];
};

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
