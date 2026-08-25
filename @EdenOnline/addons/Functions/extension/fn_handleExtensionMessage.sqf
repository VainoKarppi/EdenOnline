// EOEX_fnc_handleExtensionMessage
// Handles a single extension callback message.
// Params: [_function, _data] where _data is already parseSimpleArray'd.

params [["_function",""], ["_data",[]]];

if (_function == "") exitWith {};

// Rest API requests
if (_function == "ApiServerCommand") exitWith {
	// _data:[""function"",""MYTAG_fnc_test""]"
	_data params ["_type","_subFunction"];
	if (_type == "function") then {
		[_subFunction] call EOEX_fnc_reloadFunctions;
	};
};

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
	};
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
		case "StartObjectDrag": {
			[_data select 0, _data select 1] call EOEX_fnc_receiveDragStart;
		};

		case "UpdateObjectDrag": {
			[_data select 0, _data select 1] call EOEX_fnc_receiveDragUpdate;
		};

		case "EndObjectDrag": {
			[_data select 0, _data select 1] call EOEX_fnc_receiveDragEnd;
		};

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
			private _attributeMap = createHashMapFromArray (_data select 1);
			private _object = create3DENEntity ["Object", _attributeMap get "ItemClass", _attributeMap get "Position"];
			
			{
				_object set3DENAttribute [_x, _y];
			} forEach _attributeMap;

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
			[_data select 0] spawn EOEX_fnc_updateClientList;
		};

		case "SetInitialMissionAttributes": {
			private _attributes = _data select 0;

			diag_log format ["SetInitialMissionAttributes: Connected: %1", missionNamespace getVariable ["EOEX_var_Connected",false]];
			// TODO block this script to execute add3DENEventHandler ["OnEntityAttributeChanged", {}];
			set3DENMissionAttributes _attributes;
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
