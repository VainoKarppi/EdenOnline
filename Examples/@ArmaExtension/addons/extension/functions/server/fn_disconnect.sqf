
// TODO dont disconnect before sync has been made

params ["_type"];

diag_log "Disconnecting from 3DEN Online...";

private _result = EXT_var_extensionName callExtension "Disconnect";

// variables
missionNamespace setVariable ["EXT_var_clientID",nil];
missionNamespace setVariable ["EXT_var_Connected",false];

// reset variables
EXT_var_extensionResponse = [];
EXT_var_extensionIDs = [];


// 0 == server shutdown
// 1 == user shutdown
/*
if (_type == 0) then {
	["Server shutdown!",0,5] call BIS_fnc_3DENNotification;
} else {
	_request = ["Disconnect",[]];
	_return = [_request,true] call EOE_fnc_callExtensionAsync;
	if (_return isEqualTo false) exitWith {(findDisplay 0) closeDisplay 0}; // If it didnt disconnect succesfully, then force...
	diag_log "Disconnected successfully!";
};
*/

/*
test = false;
[] spawn {
	while {test} do {
		_list = (uinamespace getvariable ["bis_fnc_3DENControlsHint_place",[""]]);
		if !(_list isEqualTo [""]) then {
			systemChat str(_list);
		}
	};
};
*/