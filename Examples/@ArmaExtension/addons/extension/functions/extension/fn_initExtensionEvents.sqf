

if (isNil "EXT_var_extensionName" || isNil "EXT_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

addMissionEventHandler ["ExtensionCallback",{
	params [["_name",""],["_function",""],["_data","[]"]];
	if (_name == "" || _function == "") exitWith {};

	
	if (_name == EXT_var_extensionName) then {

		_data = parseSimpleArray _data;

		// Extension is requesting for data from arma
		if (_function select [0,8] == "REQUEST|") exitWith {
			(_function splitString "|") params ["_request", "_method",["_requestID","-1"]];
			diag_log format ["EXTENSION REQUESTING DATA > _method=%1, _requestID:%2, _data=%3", _method, _requestID, _data];

			// TODO if method does not contain _fnc_, then its a raw code to be executed getVariable _method;
			_code = missionNamespace getVariable _method;
			if (!isNil "_method") then {
				_response = _data call _code;
				EXT_var_extensionName callExtension [format ["ARMA_RESPONSE|%1", _requestID], _response];
			} else {
				_response = _data call compile _method;
				EXT_var_extensionName callExtension [format ["ARMA_RESPONSE|%1", _requestID], _response];
			}

			
		};
		

		if (_function != "CameraUpdate" && _function != "ASYNC_RESPONSE") then {
			diag_log "=========================================================================================";
			diag_log _function;
			diag_log _data;
			diag_log "=========================================================================================";
		};

		(_function splitString "|") params ["_method",["_requestID","-1"],["_returnCode","1"]];


		// Is data to be returned
		if (_method == "ASYNC_RESPONSE") then {
			_requestID = parseNumber _requestID;
			_returnCode = parseNumber _returnCode;
			
			if (_requestID == -1) exitWith { diag_log "ERROR: Async Key not included in response!" };

			if !(_requestID in EXT_var_extensionRequests) exitWith { diag_log format ["ERROR: ID %1 not found!", _requestID] };
			
			EXT_var_extensionResponses set [_requestID,[_data,_returnCode]];
			
		} else {
			// IS data that we need to process (call in)
			switch (_method) do {
				case "LoadingScreen": {
					_enable = _data select 0;
					_progress = _data select 1;
					if (_enable) then {
						if (isNil "EXT_var_loadingScreen") then {
							startLoadingScreen ["New client connecting..."];
						};
						EXT_var_loadingScreen = true;
						progressLoadingScreen _progress;
					} else {
						endLoadingScreen;
						EXT_var_loadingScreen = nil;
					};
				};
				case "ObjectSyncCount": {
					EXT_var_expectedObjectSyncCount = _data select 0;
				};

				case "ObjectSyncData": {
					private _id = _data select 0;
					private _map = createHashMapFromArray (_data select 1);
					private _object = create3DENEntity ["Object", _map get "ItemClass", _map get "Position"];
					_object setVariable ["EXT_objectID",_id];
				};

				case "ObjectCreated": {
					private _id = _data select 0;
					private _map = createHashMapFromArray (_data select 1);
					private _object = create3DENEntity ["Object", _map get "ItemClass", _map get "Position"];
					_object setVariable ["EXT_objectID",_id];
				};

				case "ObjectUpdated": {
					private _id = _data select 0;
					private _map = createHashMapFromArray (_data select 1);

					{
						private _objId = _x getVariable "EXT_objectID";
						if (!isNil "_objId" && _objId == _id) then { // TODO Replace with exitWith when release
							private _object = _x;
							_object setVariable ["EXT_updateRequested", true];
							{
								if (isNil "_x" || isNil "_y") then { continue };
								_success = _object set3DENAttribute [_x, _y];
								if !(_success) then { diag_log "ERROR: INVALID ATTRIBUTES" };
							} forEach _map;
						};
					} forEach (all3DENEntities # 0);
				};

				case "ObjectRemoved": {
					_object setVariable ["EXT_updateRequested", true];
					private _id = _data select 0;
					private _objects = ((all3DENEntities # 0) select { _x getVariable ["EXT_objectID","-1"] == _id });
					delete3DENEntities _objects;
				};

				case "CameraUpdate": {
					private _id = _data select 0;
					private _position = _data select 1;
					private _direction = _data select 2;
					
					_cameras = uiNamespace getVariable ["EXT_var_networkCameras", createHashMap];
					_cameras set [_id, [_position,_direction]];
				};

				case "UpdateClientList": {
					// Updates player list and their names.
					// [[id,"name1"],[id,"name2"]]
					
					private _otherClients = _data select 0;
					EXT_var_OtherClients = createHashMapFromArray _otherClients;


					// Remove cameras belonging to clients that no longer exist
					private _networkCameras = uiNamespace getVariable ["EXT_var_networkCameras", createHashMap];
					{
						if !(_x in EXT_var_OtherClients) then {
							_networkCameras deleteAt _x;
						};
					} forEach keys _networkCameras;

					uiNamespace setVariable ["EXT_var_networkCameras", _networkCameras];


					// Update client list
					[] spawn EXT_fnc_showPlayersDialog;
				};

				case "SetInitialMissionAttributes": {
					private _data = _data select 0;

					diag_log format ["SetInitialMissionAttributes: Connected: %1", missionNamespace getVariable ["EXT_var_Connected",false]];
					// TODO block this script to execute add3DENEventHandler ["OnEntityAttributeChanged", {}];
					set3DENMissionAttributes _data;
				};

				case "SetMissionAttribute": {
					private _section = _data select 0;
					private _property = _data select 1;
					private _value = _data select 2;

					_section set3DENMissionAttribute [_property, _value];
				};

				default {
					diag_log format ["ERROR: _method:%1, _data:%2", _method, _data];
				};
			};
		};
	};
}];


EXT_var_eventsReady = true;