
// 0 == server shutdown
// 1 == user shutdown

params [["_type",1],["_reason",""]];

diag_log "Disconnecting from 3DEN Online...";

// Call Disconnect. Dont disconnect for real, when using DEBUg. This allows us to rejoin as a "second" person to server.
if (missionNamespace getVariable ["EOEX_var_Connected",false] && !(missionNamespace getVariable ["EOEX_var_DEBUG",false])) then {
	["Disconnect", []] spawn EOEX_fnc_callExtensionAsync; // Disconnect can only be called using async key!
};

// Re-enable Play buttons
[true] call EOEX_fnc_togglePlayButtons;

// variables
missionNamespace setVariable ["EOEX_var_clientID",nil];
missionNamespace setVariable ["EOEX_var_IsHost", false];
missionNamespace setVariable ["EOEX_var_extensionResponse", []];
missionNamespace setVariable ["EOEX_var_extensionIDs", []];

// Remove player list
ctrlDelete (uiNamespace getVariable ["EOEX_var_PlayerListDialog",controlNull]);
uiNamespace setVariable ["EOEX_var_PlayerListDialog",nil];


// Remove camera object draws
["EOEX_var_CameraDrawEvent", "onEachFrame"] call BIS_fnc_removeStackedEventHandler;
((findDisplay 313) displayCtrl 51) ctrlRemoveEventHandler ["Draw", missionNamespace getVariable ["EOEX_var_MAPCTRL", -1]];

if (missionNamespace getVariable ["EOEX_var_Connected",false]) then {
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

missionNamespace setVariable ["EOEX_var_Connected",false];

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