

if (isNil "EXT_var_extensionName" || isNil "EXT_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

addMissionEventHandler ["ExtensionCallback",{
	params [["_name",""],["_function",""],["_data","[]"]];
	if (_name == "" || _function == "") exitWith {};

	
	if (_name == EXT_var_extensionName) then {

		_data = parseSimpleArray _data;

		(_function splitString "|") params ["_method",["_requestID","-1"],["_returnCode","1"]];

		diag_log format ["RESPONSE > _method=%1, _requestID:%2, _returnCode:%3, _data=%4", _method, _requestID, _returnCode, _data];


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
				case "ObjectSyncCount": {
					diag_log "ObjectSyncCount";
					EXT_var_expectedObjectSyncCount = _data select 0;
				};

				case "ObjectSync": {
					diag_log "ObjectSync";
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
					private _id = _data select 0;

					private _objects = ((all3DENEntities # 0) select { _x getVariable ["EXT_objectID","-1"] == _id });
					diag_log format ["%1", _objects];

					delete3DENEntities _objects;
				};

				case "CameraUpdate": {
					private _id = _data select 0;
					private _position = _data select 1;
					private _direction = _data select 2;
					
					_cameras = uiNamespace getVariable ["EXT_var_networkCameras", createHashMap];
					_cameras set [_id, [_position,_direction]];
				};

				default {
					diag_log format ["ERROR: _method:%1, _data:%2", _method, _data];
				};
			};
		};
	};
}];


EXT_var_eventsReady = true;