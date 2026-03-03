

if (isNil "EXT_var_extensionName" || isNil "EXT_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

addMissionEventHandler ["ExtensionCallback",{
	params [["_name",""],["_function",""],["_data","[]"]];
	if (_name == "" || _function == "") exitWith {};

	
	if (_name == EXT_var_extensionName) then {

		_data = parseSimpleArray _data;

		(_function splitString "|") params ["_method",["_requestID","-1"],["_returnCode","1"]];

		diag_log format ["_method=%1, _requestID:%2, _returnCode:%3, _data=%4", _method, _requestID, _returnCode, _data];


		// Is data to be returned
		if (_method == "ASYNC_RESPONSE") then {
			_requestID = parseNumber _requestID;
			_returnCode = parseNumber _returnCode;

			if (_returnCode != 0) exitWith { diag_log format ["ERROR: %1", _data] };
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
				};

				case "ObjectCreated": {
					diag_log "ObjectCreated";
				};

				default {
					diag_log format ["ERROR: _method:%1, _data:%2", _method, _data];
				};
			};
		};
	};
}];


EXT_var_eventsReady = true;