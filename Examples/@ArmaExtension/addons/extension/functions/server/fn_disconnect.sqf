
// 0 == server shutdown
// 1 == user shutdown

params [["_type",1],["_reason",""]];

diag_log "Disconnecting from 3DEN Online...";

// Call Disconnect. Dont disconnect for real, when using DEBUg. This allows us to rejoin as a "second" person to server.
if (missionNamespace getVariable ["EXT_var_Connected",false] && !(missionNamespace getVariable ["EXT_var_DEBUG",false])) then {
	["Disconnect", []] spawn EXT_fnc_callExtensionAsync; // Disconnect can only be called using async key!
};



// variables
missionNamespace setVariable ["EXT_var_clientID",nil];
missionNamespace setVariable ["EXT_var_IsHost", false];
missionNamespace setVariable ["EXT_var_extensionResponse", []];
missionNamespace setVariable ["EXT_var_extensionIDs", []];

// Remove player list
ctrlDelete (uiNamespace getVariable ["EXT_var_PlayerListDialog",controlNull]);
uiNamespace setVariable ["EXT_var_PlayerListDialog",nil];


// Remove camera object draws
["EXT_var_GUIDISPLAY", "onEachFrame"] call BIS_fnc_removeStackedEventHandler;
((findDisplay 313) displayCtrl 51) ctrlRemoveEventHandler ["Draw", missionNamespace getVariable ["EXT_var_MAPCTRL", -1]];

if (missionNamespace getVariable ["EXT_var_Connected",false]) then {
	// If there is no reason, it was successful
	if (_reason == "") then {
		if (_type == 0) then {
			["Server was shutdown!",0,5] call BIS_fnc_3DENNotification;
		} else {
			["You disconnected!",0,5] call BIS_fnc_3DENNotification;
		};
	} else {
		if (_type == 0) then {
			[format ["Server was shutdown! Reason: %1", _reason], 1, 5] call BIS_fnc_3DENNotification;
		} else {
			[format ["You disconnected! Reason: %1", _reason], 1, 5] call BIS_fnc_3DENNotification;
		};
	};
};

missionNamespace setVariable ["EXT_var_Connected",false];

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