// EOEX_fnc_initExtensionEvents


if (isNil "EOEX_var_extensionName" || isNil "EOEX_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

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


		if (EOEX_var_DEBUG && {!(_method in ["CameraUpdate", "ASYNC_RESPONSE", "ObjectSyncBatch", "CreateSyncConnectionBatch"])}) then {
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
					EOEX_var_ApplyingRemoteChanges = true;
					[_data select 0, _data select 1, _data select 2] call EOEX_fnc_onReceiveCreateSyncConnection;
					EOEX_var_ApplyingRemoteChanges = false;
				};

				case "CreateSyncConnectionBatch": {
					EOEX_var_ApplyingRemoteChanges = true;
					{
						_x params ["_fromID", "_toID", "_type"];
						[_fromID, _toID, _type, true] call EOEX_fnc_onReceiveCreateSyncConnection;
					} forEach (_data select 0);
					EOEX_var_ApplyingRemoteChanges = false;
				};

				case "RemoveSyncConnection": {
					EOEX_var_ApplyingRemoteChanges = true;
					[_data select 0, _data select 1, _data select 2] call EOEX_fnc_onReceiveRemoveSyncConnection;
					EOEX_var_ApplyingRemoteChanges = false;
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

				case "ObjectSyncData": {
					EOEX_var_ApplyingRemoteChanges = true;
					private _id = _data select 0;
					private _attributeMap = createHashMapFromArray (_data select 1);
					private _object = create3DENEntity ["Object", _attributeMap get "ItemClass", _attributeMap get "Position"];
					
					{
						_object set3DENAttribute [_x, _y];
					} foreach _attributeMap;

					_object setVariable ["EOEX_var_objectID",_id];
					EOEX_var_Objects set [_id, _object];
					EOEX_var_ApplyingRemoteChanges = false;
				};

				case "ObjectSyncBatch": {
					EOEX_var_ApplyingRemoteChanges = true;
					{
						_x params ["_id", "_attributes"];
						private _attributeMap = createHashMapFromArray _attributes;
						private _object = create3DENEntity ["Object", _attributeMap get "ItemClass", _attributeMap get "Position"];

						{
							_object set3DENAttribute [_x, _y];
						} forEach _attributeMap;

						_object setVariable ["EOEX_var_objectID", _id];
						EOEX_var_Objects set [_id, _object];
					} forEach (_data select 0);
					EOEX_var_ApplyingRemoteChanges = false;
				};

				case "ObjectCreated": {
					EOEX_var_ApplyingRemoteChanges = true;
					private _id = _data select 0;
					private _map = createHashMapFromArray (_data select 1);
					private _object = create3DENEntity ["Object", _map get "ItemClass", _map get "Position"];
					_object setVariable ["EOEX_var_objectID",_id];
					EOEX_var_Objects set [_id, _object];
					EOEX_var_ApplyingRemoteChanges = false;
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

					if (!isNull _object) then {
						EOEX_var_ApplyingRemoteChanges = true;
						_object setVariable ["EOEX_updateRequested", true];
						{
							if (isNil "_x" || isNil "_y") then { continue };
							private _success = _object set3DENAttribute [_x, _y];
							if !(_success) then { diag_log "ERROR: INVALID ATTRIBUTES" };
						} forEach _map;
						EOEX_var_ApplyingRemoteChanges = false;
					};
				};

				case "ObjectRemoved": {
					EOEX_var_ApplyingRemoteChanges = true;
					private _id = _data select 0;
					private _object = EOEX_var_Objects getOrDefault [_id, objNull];
					if (isNull _object) then {
						private _objects = ((all3DENEntities # 0) select { _x getVariable ["EOEX_var_objectID", ""] == _id });
						delete3DENEntities _objects;
					} else {
						delete3DENEntities [_object];
					};
					EOEX_var_Objects deleteAt _id;
					EOEX_var_ApplyingRemoteChanges = false;
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
