

if (isNil "EOEX_var_extensionName" || isNil "EOEX_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

addMissionEventHandler ["ExtensionCallback",{
	params [["_name",""],["_function",""],["_data","[]"]];
	if (_name == "" || _function == "") exitWith {};

	
	if (_name == EOEX_var_extensionName) then {

		_data = parseSimpleArray _data;

		// Extension is requesting for data from arma
		if (_function select [0,8] == "REQUEST|") exitWith {
			(_function splitString "|") params ["_request", "_method",["_requestID","-1"]];
			diag_log format ["EXTENSION REQUESTING DATA > _method=%1, _requestID:%2, _data=%3", _method, _requestID, _data];

			// TODO if method does not contain _fnc_, then its a raw code to be executed getVariable _method;
			_code = missionNamespace getVariable _method;
			if (!isNil "_method") then {
				_response = _data call _code;
				EOEX_var_extensionName callExtension [format ["ARMA_RESPONSE|%1", _requestID], _response];
			} else {
				_response = _data call compile _method;
				EOEX_var_extensionName callExtension [format ["ARMA_RESPONSE|%1", _requestID], _response];
			}

			
		};
		

		(_function splitString "|") params ["_method",["_requestID","-1"],["_returnCode","1"]];


		if (_method != "CameraUpdate" && _method != "ASYNC_RESPONSE") then {
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
					[_data select 0, _data select 1, _data select 2] call EOEX_fnc_onReceiveCreateSyncConnection;
				};

				case "RemoveSyncConnection": {
					[_data select 0, _data select 1, _data select 2] call EOEX_fnc_onReceiveRemoveSyncConnection;
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
					private _id = _data select 0;
					private _map = createHashMapFromArray (_data select 1);
					private _object = create3DENEntity ["Object", _map get "ItemClass", _map get "Position"];
					_object setVariable ["EOEX_var_objectID",_id];
				};

				case "ObjectCreated": {
					private _id = _data select 0;
					private _map = createHashMapFromArray (_data select 1);
					private _object = create3DENEntity ["Object", _map get "ItemClass", _map get "Position"];
					_object setVariable ["EOEX_var_objectID",_id];
				};

				case "ObjectUpdated": {
					private _id = _data select 0;
					private _map = createHashMapFromArray (_data select 1);

					{
						private _objId = _x getVariable "EOEX_var_objectID";
						if (!isNil "_objId" && _objId == _id) exitWith {
							private _object = _x;
							_object setVariable ["EOEX_updateRequested", true];
							{
								if (isNil "_x" || isNil "_y") then { continue };
								_success = _object set3DENAttribute [_x, _y];
								if !(_success) then { diag_log "ERROR: INVALID ATTRIBUTES" };
							} forEach _map;
						};
					} forEach (all3DENEntities # 0);
				};

				case "ObjectRemoved": {
					private _id = _data select 0;
					private _objects = ((all3DENEntities # 0) select { _x getVariable ["EOEX_var_objectID","-1"] == _id });
					delete3DENEntities _objects;
				};

				case "CameraUpdate": {
					private _id = _data select 0;
					private _position = _data select 1;
					private _direction = _data select 2;
					
					_cameras = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap];
					_cameras set [_id, [_position,_direction]];
				};

				case "UpdateClientList": {
					// Updates player list and their names.
					// [[id,"name1"],[id,"name2"]]

					// Make an independent copy of the previous client list
					private _previousClients = missionNamespace getVariable ["EOEX_var_OtherClients", createHashMap];

					_previousClients = +_previousClients;

					// Create the new client list
					private _otherClients = _data select 0;
					private _newClients = createHashMapFromArray _otherClients;

					// Detect newly connected clients
					{
						private _clientId = _x;

						if !(_clientId in _previousClients) then {
							private _username = _newClients get _clientId;

							diag_log format ["[EXTENSION] Client connected: %1 (%2)", _clientId, _username];
							systemChat format ["Client connected: %1 (%2)", _clientId, _username];
						};
					} forEach keys _newClients;

					// Detect disconnected clients and remove their cameras
					private _networkCameras = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap];

					{
						private _clientId = _x;

						if !(_clientId in _newClients) then {
							private _username = _previousClients getOrDefault [_clientId, format ["Client %1", _clientId]];

							_networkCameras deleteAt _clientId;

							diag_log format ["[EXTENSION] Client disconnected: %1 (%2)", _clientId, _username];
							systemChat format ["Client disconnected: %1 (%2)", _clientId, _username];
						};
					} forEach keys _previousClients;

					// Store the new client list
					missionNamespace setVariable ["EOEX_var_OtherClients", _newClients];

					uiNamespace setVariable ["EOEX_var_networkCameras", _networkCameras];

					// Update client list UI
					[] spawn EOEX_fnc_showPlayersDialog;
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
