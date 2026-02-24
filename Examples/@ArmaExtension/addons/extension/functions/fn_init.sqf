if !(isNil "EXT_var_extensionRequests") exitWith { true }; // Extension already initialized


diag_log "Initializing Extension Test for C# .NET";

// Init variables
EXT_var_extensionName = "ArmaExtension";
EXT_var_eventsReady = false;

private _result = EXT_var_extensionName callExtension "version";
if (_result == "") exitWith { false }; // Extension not found. Already logged to .RPT

private _return = [];
if (_result isEqualType []) then { // Params used
	_return = (parseSimpleArray _result) select 0;
} else {
	_return = parseSimpleArray _result;
};

private _data = (_return select 1) select 0;

if (_return select 0 == "ERROR") exitWith { diag_log format ["ERROR: ", _data]; false };

EXT_var_extensionResponses = createHashMap;
EXT_var_extensionRequests = createHashMap;
diag_log formatText ["VERSION: %1",_data];

call EXT_fnc_initEvents;

true