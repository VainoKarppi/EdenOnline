
// ["Numeric",[10+10]] call EXT_fnc_callExtensionAsync



params [["_function","",[""]],["_arguments",[],[[]]],["_fireAndForget",false,[false]],["_timeout",1,[0]]];

if (!canSuspend) exitWith {_this call EXT_fnc_callExtension};

if (isNil "EXT_var_extensionRequests") then {
	private _initSuccess = call EXT_fnc_init;
	if (!_initSuccess) exitWith {};
};

// Insert request id to function
private _requestId = if (_fireAndForget) then {-1} else {ceil(random 99999)};

EXT_var_extensionRequests set [_requestId, _function];

_function = _function + "|" + str(_requestId); // Add ASYNC key to request

private _request = [_function ,_arguments];

// Call Extension
diag_log formatText ["REQUEST ASYNC: %1",_request];
private _result = EXT_var_extensionName callExtension _request;
if (_result isEqualTo "" || _fireAndForget) exitWith { EXT_var_extensionRequests deleteAt _requestId }; // Extension not found or fire and forget.


private _return = [];
if (_result isEqualType []) then {
	_return = parseSimpleArray (_result select 0);
} else {
	_return = parseSimpleArray _result;
};

_return params ["_returnMessage","_returnData"];


if (_returnMessage == "ERROR") exitWith {
	EXT_var_extensionRequests deleteAt _requestId;
	diag_log formatText ["ERROR: %1", _return select 1];
};

diag_log formatText ["WAITING FOR REPSONSE FOR REQUEST: %1", _requestId];



_return = nil;

private _success = false;
private _startTime = diag_tickTime;
_returnData = format ["ERROR: Request (%1) timed out!",_function];

private _loop = 1;
while {(diag_tickTime - _startTime) < _timeout} do {
	if !(_requestId in EXT_var_extensionRequests) exitWith { diag_log "ERROR: Request has been canceled!" };
	_return = EXT_var_extensionResponses get _requestId;

	if (!isNil "_return") exitWith {
		_returnData = _return select 0;
		_success = (_return select 1) == 0;
	};
	diag_log format ["LOOP: %1", _loop];
	uiSleep 0.001;
	_loop = _loop + 1;
};

//EXT_var_extensionResponses deleteAt _requestId;
//EXT_var_extensionRequests deleteAt _requestId;

if !(_success) exitWith { diag_log formatText ["ERROR: %1", _returnData]};

diag_log formatText ["SUCCESS WITH DATA: %1",_returnData];

_returnData