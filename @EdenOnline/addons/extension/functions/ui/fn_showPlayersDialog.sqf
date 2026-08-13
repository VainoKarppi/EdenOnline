
/*
_cameras = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap];
_cameras set [2, [[0,0,0],[1,1,1]]];
_cameras set [3, [[100,100,100],[1,1,1]]];
uiNamespace setVariable ["EOEX_var_networkCameras", _cameras];

EOEX_var_OtherClients = createHashMapFromArray [[2,"Razer"]];
*/

if (isNil {uiNamespace getVariable "EOEX_var_networkCameras"} || isNil {missionNamespace getVariable "EOEX_var_OtherClients"}) exitWith {
    diag_log "ERROR: Unable to create player list GUI. >> Invalid variables!"
};

// Clear existing list, and create new
ctrlDelete (uiNamespace getVariable ["EOEX_var_PlayerListDialog", controlNull]);
uiNamespace setVariable ["EOEX_var_PlayerListDialog", nil];

if (count EOEX_var_OtherClients == 0) exitWith { diag_log "No other players on server, hiding player list GUI!" };

disableSerialization;

// Get Eden display
private _display = findDisplay 313;

// Create listbox
private _list = _display ctrlCreate ["RscListbox", 33900];
_list ctrlSetPosition [
    safeZoneX + safeZoneW - 0.47,
    safeZoneY + 0.09,
    0.20,
    0.3
];
_list ctrlSetScale 0.83;
_list ctrlCommit 0;





// Populate list
{
    _list lbAdd (_y);
    _list lbSetData [_forEachIndex, str(_x)];
} forEach EOEX_var_OtherClients;

// Select first item
_list lbSetCurSel 0;

// Handle selection changes
_list ctrlAddEventHandler ["LBSelChanged", {
    params ["_list", "_selectedIndex"];

    private _clientID = parseNumber (_list lbData _selectedIndex);

    private _cameraData = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap] get _clientID;
    if (isNil "_cameraData") exitWith {};

    private _targetCameraPos = _cameraData # 0;

    private _camera = get3DENCamera;
    private _dir = [getPosASL _camera, _targetCameraPos] call BIS_fnc_dirTo;

    _camera setDir _dir;
	private _cameraPos = getPosATL _camera;

	private _dx = (_targetCameraPos # 0) - (_cameraPos # 0);
	private _dy = (_targetCameraPos # 1) - (_cameraPos # 1);
	private _dz = (_cameraPos # 2) - (_targetCameraPos # 2);

	private _horizontalDistance = sqrt (_dx * _dx + _dy * _dy);

	// Positive = looking up, negative = looking down
	private _pitch = -(_dz atan2 _horizontalDistance);

	[_camera, _pitch, 0] call BIS_fnc_setPitchBank;
}];

_list ctrlAddEventHandler ["LBDblClick", {
    params ["_list", "_selectedIndex"];

    private _clientID = parseNumber (_list lbData _selectedIndex);

    private _cameraData = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap] get _clientID;
    if (isNil "_cameraData") exitWith {};

    private _targetCameraPos = _cameraData # 0;

    private _camera = get3DENCamera;

    // Original camera position
    private _originalPos = getPosATL _camera;

    // Direction from target towards the original camera position
    private _dir = [_targetCameraPos, _originalPos] call BIS_fnc_dirTo;

    // Position X m behind the target (relative to the original camera)
	private _distance = 100;
    private _camPos = _targetCameraPos getPos [_distance, _dir];
    _camPos set [2, (_targetCameraPos # 2) + 30];

	if (_camera distance _targetCameraPos > _distance + 5) then {
    	_camera setPosATL _camPos;
	};

    _camera setDir (_dir + 180);
	private _cameraPos = getPosATL _camera;

	private _dx = (_targetCameraPos # 0) - (_cameraPos # 0);
	private _dy = (_targetCameraPos # 1) - (_cameraPos # 1);
	private _dz = (_cameraPos # 2) - (_targetCameraPos # 2);

	private _horizontalDistance = sqrt (_dx * _dx + _dy * _dy);

	// Positive = looking up, negative = looking down
	private _pitch = -(_dz atan2 _horizontalDistance);

	[_camera, _pitch, 0] call BIS_fnc_setPitchBank;
}];



_display displayAddEventHandler ["MouseButtonDown", {
    params ["_display", "_button"];

	[_button] spawn {
		params ["_button"];
		
		uiSleep 0.1;
		// Left mouse button only
		if (_button != 0) exitWith {};

		private _menu = uiNamespace getVariable ["PlayerContextMenu", controlNull];

		if (!isNull _menu) then {
			ctrlDelete _menu;
			uiNamespace setVariable ["PlayerContextMenu", controlNull];
		};
	};
}];


_list ctrlAddEventHandler ["MouseButtonDown", {
    params ["_ctrl", "_button", "_xPos", "_yPos"];

    // Right mouse button
    if (_button != 1) exitWith {};

    // Remove previous menu
    private _oldMenu = uiNamespace getVariable ["PlayerContextMenu", controlNull];
    if (!isNull _oldMenu) then {
        ctrlDelete _oldMenu;
    };

    private _display = ctrlParent _ctrl;

    // Create menu
    private _menu = _display ctrlCreate ["RscListbox", 33901];
    uiNamespace setVariable ["PlayerContextMenu", _menu];

	_ctrl lbSetValue [10,9999];

	_list lbSetCurSel -1;

    _menu ctrlSetPosition [_xPos, _yPos, 0.16, 0.12];
	_menu ctrlSetBackgroundColor [0,0,0,1];
    _menu ctrlCommit 0;

    _menu lbAdd "Teleport";
    _menu lbAdd "Kick";

    // Remember selected player
    uiNamespace setVariable [
        "PlayerContextSelection",
        lbCurSel _ctrl
    ];

    _menu ctrlAddEventHandler ["LBSelChanged", {
        params ["_menu", "_index"];

        private _playerIndex = uiNamespace getVariable ["PlayerContextSelection", -1];

        switch (_index) do {
            case 0: {
                systemChat format ["Teleport %1", _playerIndex];
            };
            case 1: {
                systemChat format ["Track %1", _playerIndex];
            };
            case 2: {
                systemChat format ["Kick %1", _playerIndex];
            };
            case 3: {
                systemChat format ["Copy UID %1", _playerIndex];
            };
        };

        ctrlDelete _menu;
        uiNamespace setVariable ["PlayerContextMenu", controlNull];
    }];
}];


uiNamespace setVariable ["EOEX_var_PlayerListDialog", _list];