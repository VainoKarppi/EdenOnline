

if (isNil "EXT_var_extensionName" || isNil "EXT_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

addMissionEventHandler ["ExtensionCallback",{
	params [["_name",""],["_function",""],["_data","[]"]];
	if (_name == "" || _function == "") exitWith {};

	if (_name == EXT_var_extensionName) then {
		diag_log format ["_function=%1, _data=%2", _function, _data];

		(_function splitString "|") params ["_type",["_requestID","-1"],["_returnCode","1"]];

		_requestID = parseNumber _requestID;
		_returnCode = parseNumber _returnCode;

		if (_type == "ASYNC_RESPONSE") then {
			
			if (_requestID == -1) exitWith { diag_log "ERROR: Async Key not included in response!" };
			if !(_requestID in EXT_var_extensionRequests) exitWith { diag_log format ["ERROR: ID %1 not found!", _requestID] };

			_data = parseSimpleArray _data;
			
			EXT_var_extensionResponses set [_requestID,[_data,_returnCode]];	
		} else {
			diag_log format ["ERROR: %1", _data select 0]
		};
	};
}];


EXT_var_eventsReady = true;