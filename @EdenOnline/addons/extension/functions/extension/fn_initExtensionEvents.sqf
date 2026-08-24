// EOEX_fnc_initExtensionEvents


if (isNil "EOEX_var_extensionName" || isNil "EOEX_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

addMissionEventHandler ["ExtensionCallback",{
	params [["_name",""],["_function",""],["_data","[]"]];
	if (_name == "" || _function == "") exitWith {};

	
	if (_name == EOEX_var_extensionName) then {
		_data = parseSimpleArray _data;

		// A batch of messages arrived as one callback: [_data] is an array
		// of [_function, _data] pairs. Unpack and dispatch each one.
		if (_function == "BATCH") exitWith {
			{
				_x params ["_subFunction", "_subData"];
				[_subFunction, _subData] call EOEX_fnc_handleExtensionMessage;
			} forEach _data;
		};

		[_function, _data] call EOEX_fnc_handleExtensionMessage;
	};
}];


EOEX_var_eventsReady = true;
