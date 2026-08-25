// EOEX_fnc_callExtension

// RETURNS [data] (ARRAY) if success NOTHING if failed!

params [["_function","",[""]],["_arguments",[],[[]]],["_fireAndForget",false,[false]]];

if (isNil "EOEX_var_extensionRequests") then {
	private _initSuccess = call EOEX_fnc_initExtension;
	if (!_initSuccess) exitWith {};
};

if (_fireAndForget) then {_function = _function + "|-1"};


// TODO Temp
if (_function != "CameraUpdate" && _function != "SetMissionAttribute") then {
	diag_log formatText ["REQUEST: %1 WITH ARGS: %2", _function, _arguments];
};

private _result = EOEX_var_extensionName callExtension [_function, _arguments];

private _return = if (_result isEqualType []) then {
	parseSimpleArray (_result select 0)
} else {
	parseSimpleArray _result
};

private _data = (_return select 1);

if (isNil "_data") exitWith {};

if (_return select 0 == "ERROR") exitWith { diag_log formatText ["ERROR: %1", _data select 0] };

diag_log formatText ["SUCCESS WITH DATA: %1", _data select 0];

_data select 0
