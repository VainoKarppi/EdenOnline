// EOEX_fnc_initExtensionEvents


if (isNil "EOEX_var_extensionName" || isNil "EOEX_var_extensionResponses") exitWith {
	diag_log "Extension not initialized yet!";
};

addMissionEventHandler ["ExtensionCallback",{
	// Copy the callback arguments before spawning so the native
    // ExtensionCallback handler can return immediately and release
    // the extension callback slot as quickly as possible.

	private _callbackData = +_this;
	_callbackData spawn {
		params [["_name",""],["_function",""],["_data","[]"]];
		if (_name == "" || _function == "") exitWith {};

		
		if (_name == EOEX_var_extensionName) then {
			_data = parseSimpleArray _data;

			// A batch of messages arrived as one callback: [_data] is an array
			// of [_function, _data] pairs. Unpack and dispatch each one.
			if (_function == "BATCH") exitWith {
				{
					// Process each message synchronously and in the exact order it was
					// received. Using call here ensures the next message is not started
					// until handleExtensionMessage has finished processing the current one.
					_x call EOEX_fnc_handleExtensionMessage;
				} forEach _data;
			};

			[_function, _data] call EOEX_fnc_handleExtensionMessage;
		};
	};
	_callbackData = nil;
}];


EOEX_var_eventsReady = true;
