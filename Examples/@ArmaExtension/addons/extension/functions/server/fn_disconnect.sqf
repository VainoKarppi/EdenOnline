
// 0 == server shutdown
// 1 == user shutdown

params ["_type"];

diag_log "Disconnecting from 3DEN Online...";

private _result = EXT_var_extensionName callExtension "Disconnect";

// variables
missionNamespace setVariable ["EXT_var_clientID",nil];
missionNamespace setVariable ["EXT_var_Connected",false];

// reset variables
EXT_var_extensionResponse = [];
EXT_var_extensionIDs = [];

// Remove camera object draws
["EXT_var_GUIDISPLAY", "onEachFrame"] call BIS_fnc_removeStackedEventHandler;
((findDisplay 313) displayCtrl 51) ctrlRemoveEventHandler ["Draw", EXT_var_MAPCTRL];

if (_type == 0) then {
	["Server was shutdown!",0,5] call BIS_fnc_3DENNotification;
} else {
	["You disconnected!",0,5] call BIS_fnc_3DENNotification;
};

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