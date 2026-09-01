// EOEX_fnc_handleExtensionMessage
// Handles a single extension callback message.
// Params: [_function, _data] where _data is already parseSimpleArray'd.

params [["_function",""], ["_data",[]]];

if (_function == "") exitWith {};

// Rest API requests
if (_function == "ApiServerCommand") exitWith {
	// _data:[""recompile"",""MYTAG_fnc_test""]"
	_data params ["_type","_subFunction"];
	if (_type == "recompile") then {
		[_subFunction] call EOEX_fnc_reloadFunctionFromFile;
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


if (_requestID == "-1" && _method != "CameraUpdate" && _method != "ASYNC_RESPONSE" && _method != "SetInitialMissionAttributes") then {
	diag_log "";
	diag_log "================================ RECIEVED DATA (CALLIN) =================================";
	diag_log format ["_function: %1, _requestID: %2", _function, _requestID];
	diag_log _data;
	diag_log "=========================================================================================";
	diag_log "";
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
			_data call EOEX_fnc_receiveDragStart;
		};

		case "UpdateObjectDrag": {
			_data call EOEX_fnc_receiveDragUpdate;
		};

		case "EndObjectDrag": {
			_data call EOEX_fnc_receiveDragEnd;
		};

		case "CreateSyncConnection": {
			_data call EOEX_fnc_receiveCreateSyncConnection;
		};

		case "RemoveSyncConnection": {
			_data call EOEX_fnc_receiveRemoveSyncConnection;
		};

		case "ServerShutdown": {
			diag_log format ["ServerShutdown: %1", _data#0];
			[1, _data#0] spawn EOEX_fnc_disconnect;
		};

		case "LoadingScreen": {
			_data call EOEX_fnc_handleLoadingScreen;
		};
		
		case "ObjectSyncCount": {
			EOEX_var_expectedObjectSyncCount = _data select 0;
		};

		case "ObjectSyncData": {
			_data call EOEX_fnc_receiveObjectCreate;
		};

		case "ObjectCreated": {
			_data call EOEX_fnc_receiveObjectCreate;
		};

		case "ObjectUpdated": {
			_data call EOEX_fnc_receiveObjectUpdate;
		};

		case "ObjectRemoved": {
			_id call EOEX_fnc_receiveObjectDelete;
			
		};

		case "CameraUpdate": {
			_data params ["_id", "_position", "_direction"];
			
			_cameras = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap];
			_cameras set [_id, [_position,_direction]];
		};

		case "UpdateClientList": {
			[_data select 0] spawn EOEX_fnc_updateClientList;
		};

		case "SetInitialMissionAttributes": {
			private _attributes = _data select 0;
			set3DENMissionAttributes _attributes;
		};

		case "SetMissionAttribute": {
			_data call EOEX_fnc_receiveMissionAtrribute;
		};

		default {
			diag_log format ["ERROR: _method:%1, _data:%2", _method, _data];
		};
	};
};
