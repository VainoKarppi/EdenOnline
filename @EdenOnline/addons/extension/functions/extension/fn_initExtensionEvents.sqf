// EOEX_fnc_initExtensionEvents


if (isNil "EOEX_var_extensionName" || isNil "EOEX_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

EOEX_fnc_reportLiveSyncFailure = {
	params ["_message"];
	if !(missionNamespace getVariable ["EOEX_var_LiveSyncFailed", false]) then {
		EOEX_var_LiveSyncFailed = true;
		[_message, 1, 10] call BIS_fnc_3DENNotification;
		if (missionNamespace getVariable ["EOEX_var_Connected", false]) then {
			[1, _message] spawn EOEX_fnc_disconnect;
		};
	};
};

// Object creation is deliberately moved out of ExtensionCallback. The callback
// only queues data; this frame-budgeted worker keeps Eden responsive for large
// snapshots and avoids deleteAt 0 / O(n^2) queue behavior by using an offset.
private _previousObjectSyncEachFrame = missionNamespace getVariable ["EOEX_var_ObjectSyncEachFrameId", -1];
if (_previousObjectSyncEachFrame >= 0) then {
	removeMissionEventHandler ["EachFrame", _previousObjectSyncEachFrame];
};

EOEX_var_ObjectSyncEachFrameId = addMissionEventHandler ["EachFrame", {
	private _queue = missionNamespace getVariable ["EOEX_var_ObjectSyncQueue", []];
	private _offset = missionNamespace getVariable ["EOEX_var_ObjectSyncQueueOffset", 0];
	private _queueCount = count _queue;
	private _objectsWereQueued = _offset < _queueCount;

	if (_offset < _queueCount) then {
		EOEX_var_ObjectSyncApplying = true;
		EOEX_var_ApplyingRemoteChanges = true;

		private _frameLimit = missionNamespace getVariable ["EOEX_var_ObjectSyncFrameLimit", 96];
		private _deadline = diag_tickTime + (missionNamespace getVariable ["EOEX_var_ObjectSyncFrameBudget", 0.006]);
		private _processedThisFrame = 0;

		while {
			_offset < _queueCount
			&& _processedThisFrame < _frameLimit
			&& diag_tickTime < _deadline
		} do {
			private _entry = _queue select _offset;
			_entry params ["_id", "_attributes", ["_snapshot", false]];

			private _removedBeforeCreation = _snapshot && {EOEX_var_PendingObjectRemovals getOrDefault [_id, false]};
			private _liveSupersedesSnapshot = _snapshot && {EOEX_var_LiveObjectIds getOrDefault [_id, false]};
			if (_removedBeforeCreation || _liveSupersedesSnapshot) then {
				EOEX_var_PendingObjectRemovals deleteAt _id;
				EOEX_var_PendingObjectUpdates deleteAt _id;
			} else {
				// A live recreation supersedes an older removal marker. Snapshot
				// entries retain the marker so a removal racing the snapshot wins.
				if (!_snapshot) then { EOEX_var_PendingObjectRemovals deleteAt _id };
				private _attributeMap = createHashMapFromArray _attributes;
				private _pendingUpdate = EOEX_var_PendingObjectUpdates getOrDefault [_id, createHashMap];
				{
					_attributeMap set [_x, _y];
				} forEach _pendingUpdate;
				EOEX_var_PendingObjectUpdates deleteAt _id;

				private _itemClass = _attributeMap getOrDefault ["ItemClass", ""];
				private _position = _attributeMap getOrDefault ["Position", [0, 0, 0]];
				if (_itemClass == "") then {
					diag_log format ["[EdenOnline] Skipped object %1 because ItemClass is missing.", _id];
					if (_snapshot) then {
						EOEX_var_ObjectSyncFailedCount = EOEX_var_ObjectSyncFailedCount + 1;
					} else {
						["Live synchronization failed: an object class is missing."] call EOEX_fnc_reportLiveSyncFailure;
					};
				} else {
					private _object = EOEX_var_Objects getOrDefault [_id, objNull];
					if (isNull _object) then {
						_object = create3DENEntity ["Object", _itemClass, _position];
					};
					if (isNull _object) then {
						diag_log format ["[EdenOnline] Failed to create synchronized object %1 (%2).", _id, _itemClass];
						if (_snapshot) then {
							EOEX_var_ObjectSyncFailedCount = EOEX_var_ObjectSyncFailedCount + 1;
						} else {
							["Live synchronization failed: an object could not be created."] call EOEX_fnc_reportLiveSyncFailure;
						};
					} else {
						// Set identity before attributes. Delayed OnEditableEntityAdded
						// handlers can then recognize this as a remote object.
						_object setVariable ["EOEX_var_objectID", _id];
						EOEX_var_Objects set [_id, _object];
						if (!_snapshot) then { EOEX_var_LiveObjectIds set [_id, true] };

						{
							if (isNil "_x" || isNil "_y") then { continue };
							_object set3DENAttribute [_x, _y];
						} forEach _attributeMap;
					};
				};
			};

			_offset = _offset + 1;
			_processedThisFrame = _processedThisFrame + 1;
			if (_snapshot) then {
				EOEX_var_ObjectSyncProcessedCount = EOEX_var_ObjectSyncProcessedCount + 1;
			};
		};

		EOEX_var_ObjectSyncQueueOffset = _offset;
		if (_offset >= _queueCount) then {
			EOEX_var_ObjectSyncQueue = [];
			EOEX_var_ObjectSyncQueueOffset = 0;
			EOEX_var_ObjectSyncReleaseFrame = diag_frameNo + 2;
		};
	};

	// Connections must be created only after their endpoint objects exist. They
	// use their own offset queue and the same bounded per-frame policy.
	private _connectionQueue = missionNamespace getVariable ["EOEX_var_ConnectionSyncQueue", []];
	private _connectionOffset = missionNamespace getVariable ["EOEX_var_ConnectionSyncQueueOffset", 0];
	private _connectionQueueCount = count _connectionQueue;
	if (!_objectsWereQueued && {_connectionOffset < _connectionQueueCount}) then {
		EOEX_var_ObjectSyncApplying = true;
		EOEX_var_ApplyingRemoteChanges = true;

		private _connectionDeadline = diag_tickTime + (missionNamespace getVariable ["EOEX_var_ObjectSyncFrameBudget", 0.006]);
		private _processedConnectionsThisFrame = 0;
		while {
			_connectionOffset < _connectionQueueCount
			&& _processedConnectionsThisFrame < 128
			&& diag_tickTime < _connectionDeadline
			&& {
				private _candidate = _connectionQueue select _connectionOffset;
				diag_tickTime >= (_candidate param [6, 0])
			}
		} do {
			(_connectionQueue select _connectionOffset) params [
				"_create", "_fromID", "_toID", "_type", ["_snapshot", false],
				["_retryCount", 0], ["_notBeforeTime", 0], ["_expiresAt", -1]
			];
			private _connectionKey = str [_fromID, _toID, _type];
			if (_create) then {
				private _removedBeforeCreation = _snapshot && {
					EOEX_var_PendingConnectionRemovals getOrDefault [_connectionKey, false]
				};
				if (_removedBeforeCreation) then {
					EOEX_var_PendingConnectionRemovals deleteAt _connectionKey;
				} else {
					if (!_snapshot) then { EOEX_var_PendingConnectionRemovals deleteAt _connectionKey };
					private _connectionCreated = [_fromID, _toID, _type, true] call EOEX_fnc_onReceiveCreateSyncConnection;
					if !(_connectionCreated) then {
						if (_snapshot) then {
							EOEX_var_ConnectionSyncFailedCount = EOEX_var_ConnectionSyncFailedCount + 1;
						} else {
							if (_expiresAt < 0) then { _expiresAt = diag_tickTime + 35 };
							if (diag_tickTime < _expiresAt) then {
								private _retryDelay = 0.05 * (2 ^ (_retryCount min 3));
								_connectionQueue pushBack [
									true, _fromID, _toID, _type, false,
									_retryCount + 1, diag_tickTime + _retryDelay, _expiresAt
								];
							} else {
								["Live synchronization failed: connection endpoints did not arrive."] call EOEX_fnc_reportLiveSyncFailure;
							};
						};
					};
				};
			} else {
				if (EOEX_var_SyncConnectionKeys getOrDefault [_connectionKey, false]) then {
					[_fromID, _toID, _type] call EOEX_fnc_onReceiveRemoveSyncConnection;
				};
				EOEX_var_PendingConnectionRemovals set [_connectionKey, true];
			};
			_connectionOffset = _connectionOffset + 1;
			_processedConnectionsThisFrame = _processedConnectionsThisFrame + 1;
			if (_snapshot) then {
				EOEX_var_ConnectionSyncProcessedCount = EOEX_var_ConnectionSyncProcessedCount + 1;
			};
		};

		EOEX_var_ConnectionSyncQueueOffset = _connectionOffset;
		if (_connectionOffset >= count _connectionQueue) then {
			EOEX_var_ConnectionSyncQueue = [];
			EOEX_var_ConnectionSyncQueueOffset = 0;
			EOEX_var_ObjectSyncReleaseFrame = diag_frameNo + 2;
		};
	};

	if (
		missionNamespace getVariable ["EOEX_var_ObjectSyncApplying", false]
		&& {count (missionNamespace getVariable ["EOEX_var_ObjectSyncQueue", []]) == 0}
		&& {count (missionNamespace getVariable ["EOEX_var_ConnectionSyncQueue", []]) == 0}
		&& {
			EOEX_var_expectedObjectSyncCount < 0
			|| {
				EOEX_var_expectedConnectionSyncCount >= 0
				&& EOEX_var_ObjectSyncProcessedCount >= EOEX_var_expectedObjectSyncCount
				&& EOEX_var_ConnectionSyncProcessedCount >= EOEX_var_expectedConnectionSyncCount
			}
		}
		&& {diag_frameNo >= (missionNamespace getVariable ["EOEX_var_ObjectSyncReleaseFrame", -1])}
	) then {
		EOEX_var_ObjectSyncApplying = false;
		EOEX_var_ApplyingRemoteChanges = false;
		EOEX_var_ObjectSyncReleaseFrame = -1;
	};
}];

addMissionEventHandler ["ExtensionCallback",{
	params [["_name",""],["_function",""],["_data","[]"]];
	if (_name == "" || _function == "") exitWith {};

	
	if (_name == EOEX_var_extensionName) then {
		_data = parseSimpleArray _data;

		// Rest API requests
		if (_function == "ApiServerCommand") exitWith {
			diag_log _function;
			diag_log _data;
			// [RPT] 17:30:07 "ERROR: _method:ApiServerCommand, _data:[""function"",""MYTAG_fnc_test"",""hint ""]"
			_data params ["_type","_function","_code"];
			if (_type == "function") then {
				missionNamespace setVariable [_function, compile _code];
			};
		};

		// Extension is requesting for data from arma
		if (_function select [0,8] == "REQUEST|") exitWith {
			(_function splitString "|") params ["_request", "_method",["_requestID","-1"]];
			if (EOEX_var_DEBUG) then {
				diag_log format ["EXTENSION REQUESTING DATA > _method=%1, _requestID:%2, _data=%3", _method, _requestID, _data];
			};

			// TODO if method does not contain _fnc_, then its a raw code to be executed getVariable _method;
			_code = missionNamespace getVariable _method;
			if (!isNil "_method") then {
				_response = _data call _code;
				EOEX_var_extensionName callExtension [format ["ARMA_RESPONSE|%1", _requestID], _response];
			} else {
				_response = _data call compile _method;
				EOEX_var_extensionName callExtension [format ["ARMA_RESPONSE|%1", _requestID], _response];
			};
		};
		

		(_function splitString "|") params ["_method",["_requestID","-1"],["_returnCode","1"]];

		if (
			!(missionNamespace getVariable ["EOEX_var_AcceptSyncCallbacks", false])
			&& {_method in [
				"ObjectSyncCount", "ConnectionSyncCount", "ObjectSyncData", "ObjectSyncBatch",
				"ObjectCreated", "ObjectCreatedBatch", "ObjectUpdated", "ObjectRemoved",
				"CreateSyncConnection", "CreateSyncConnectionBatch", "ConnectionSyncBatch",
				"RemoveSyncConnection"
			]}
		) exitWith {};


		if (EOEX_var_DEBUG && {!(_method in ["CameraUpdate", "ASYNC_RESPONSE", "ObjectSyncBatch", "ObjectCreatedBatch", "CreateSyncConnectionBatch", "ConnectionSyncBatch"])}) then {
			diag_log "=========================================================================================";
			diag_log _function;
			diag_log _data;
			diag_log "=========================================================================================";
		};


		// Is data to be returned
		if (_method == "ASYNC_RESPONSE") then {
			_requestID = parseNumber _requestID;
			_returnCode = parseNumber _returnCode;
			
			if (_requestID == -1) exitWith { diag_log "ERROR: Async Key not included in response!" };

			if !(_requestID in EOEX_var_extensionRequests) exitWith { diag_log format ["ERROR: ID %1 not found!", _requestID] };
			
			EOEX_var_extensionResponses set [_requestID,[_data,_returnCode]];
			
		} else {
			// IS data that we need to process (call in)
				switch (_method) do {
				case "CreateSyncConnection": {
					EOEX_var_ConnectionSyncQueue pushBack [true, _data select 0, _data select 1, _data select 2, false];
					EOEX_var_ObjectSyncApplying = true;
					EOEX_var_ApplyingRemoteChanges = true;
				};

				case "CreateSyncConnectionBatch": {
					{
						EOEX_var_ConnectionSyncQueue pushBack [true, _x select 0, _x select 1, _x select 2, false];
					} forEach (_data select 0);
					EOEX_var_ObjectSyncApplying = true;
					EOEX_var_ApplyingRemoteChanges = true;
				};

				case "ConnectionSyncBatch": {
					{
						EOEX_var_ConnectionSyncQueue pushBack [true, _x select 0, _x select 1, _x select 2, true];
					} forEach (_data select 0);
					EOEX_var_ObjectSyncApplying = true;
					EOEX_var_ApplyingRemoteChanges = true;
				};

				case "RemoveSyncConnection": {
					EOEX_var_ConnectionSyncQueue pushBack [false, _data select 0, _data select 1, _data select 2, false];
					EOEX_var_ObjectSyncApplying = true;
					EOEX_var_ApplyingRemoteChanges = true;
				};

				case "ServerShutdown": {
					diag_log format ["ServerShutdown: %1", _data#0];
					[1, _data#0] spawn EOEX_fnc_disconnect;
				};

				case "LoadingScreen": {
					_enable = _data select 0;
					_progress = _data select 1;
					if (_enable) then {
						if (isNil "EOEX_var_loadingScreen") then {
							startLoadingScreen ["New client connecting..."];
						};
						EOEX_var_loadingScreen = true;
						progressLoadingScreen _progress;
					} else {
						endLoadingScreen;
						EOEX_var_loadingScreen = nil;
					};
				};
				
				case "ObjectSyncCount": {
					EOEX_var_expectedObjectSyncCount = _data select 0;
				};

				case "ConnectionSyncCount": {
					EOEX_var_expectedConnectionSyncCount = _data select 0;
				};

				case "ObjectSyncData": {
					EOEX_var_ObjectSyncQueue pushBack [_data select 0, _data select 1, true];
					EOEX_var_ObjectSyncApplying = true;
					EOEX_var_ApplyingRemoteChanges = true;
				};

				case "ObjectSyncBatch": {
					{
						EOEX_var_ObjectSyncQueue pushBack [_x select 0, _x select 1, true];
					} forEach (_data select 0);
					EOEX_var_ObjectSyncApplying = true;
					EOEX_var_ApplyingRemoteChanges = true;
				};

				case "ObjectCreated": {
					EOEX_var_ObjectSyncQueue pushBack [_data select 0, _data select 1, false];
					EOEX_var_ObjectSyncApplying = true;
					EOEX_var_ApplyingRemoteChanges = true;
				};

				case "ObjectCreatedBatch": {
					{
						EOEX_var_ObjectSyncQueue pushBack [_x select 0, _x select 1, false];
					} forEach (_data select 0);
					EOEX_var_ObjectSyncApplying = true;
					EOEX_var_ApplyingRemoteChanges = true;
				};

				case "ObjectUpdated": {
					private _id = _data select 0;
					private _map = createHashMapFromArray (_data select 1);

					private _object = EOEX_var_Objects getOrDefault [_id, objNull];
					if (isNull _object) then {
						private _matches = (all3DENEntities # 0) select { _x getVariable ["EOEX_var_objectID", ""] == _id };
						if (_matches isNotEqualTo []) then {
							_object = _matches # 0;
							EOEX_var_Objects set [_id, _object];
						};
					};

					if (isNull _object) then {
						private _pendingUpdate = EOEX_var_PendingObjectUpdates getOrDefault [_id, createHashMap];
						{
							_pendingUpdate set [_x, _y];
						} forEach _map;
						EOEX_var_PendingObjectUpdates set [_id, _pendingUpdate];
					} else {
						EOEX_var_ApplyingRemoteChanges = true;
						_object setVariable ["EOEX_updateRequested", true];
						{
							if (isNil "_x" || isNil "_y") then { continue };
							private _success = _object set3DENAttribute [_x, _y];
							if !(_success) then { diag_log "ERROR: INVALID ATTRIBUTES" };
						} forEach _map;
						EOEX_var_ApplyingRemoteChanges = EOEX_var_ObjectSyncApplying;
					};
				};

				case "ObjectRemoved": {
					EOEX_var_ApplyingRemoteChanges = true;
					private _id = _data select 0;
					private _object = EOEX_var_Objects getOrDefault [_id, objNull];
					if (isNull _object) then {
						private _objects = ((all3DENEntities # 0) select { _x getVariable ["EOEX_var_objectID", ""] == _id });
						if (_objects isEqualTo []) then {
							EOEX_var_PendingObjectRemovals set [_id, true];
						} else {
							delete3DENEntities _objects;
						};
					} else {
						delete3DENEntities [_object];
					};
					EOEX_var_Objects deleteAt _id;
					EOEX_var_LiveObjectIds deleteAt _id;
					EOEX_var_ApplyingRemoteChanges = EOEX_var_ObjectSyncApplying;
				};

				case "CameraUpdate": {
					private _id = _data select 0;
					private _position = _data select 1;
					private _direction = _data select 2;
					
					_cameras = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap];
					_cameras set [_id, [_position,_direction]];
				};

				case "UpdateClientList": {
					diag_log _data;
					[_data select 0] spawn EOEX_fnc_updateClientList;
				};

				case "SetInitialMissionAttributes": {
					private _data = _data select 0;

					diag_log format ["SetInitialMissionAttributes: Connected: %1", missionNamespace getVariable ["EOEX_var_Connected",false]];
					// TODO block this script to execute add3DENEventHandler ["OnEntityAttributeChanged", {}];
					set3DENMissionAttributes _data;
				};

				case "SetMissionAttribute": {
					private _section = _data select 0;
					private _property = _data select 1;
					private _value = _data select 2;

					EOEX_var_SkipAttributeChange set [[_section, _property], _value];

					_section set3DENMissionAttribute [_property, _value];
				};

				default {
					diag_log format ["ERROR: _method:%1, _data:%2", _method, _data];
				};
			};
		};
	};
}];


EOEX_var_eventsReady = true;
