#include "\a3\3den\ui\resincl.inc"
#include "\a3\3den\ui\macros.inc"

disableSerialization;

if (!is3DEN) exitWith {false};

private _display = findDisplay IDD_DISPLAY3DEN createDisplay "EOEX_DisplayEmpty";

uiNamespace setVariable ["EOEX_ConnectDialog_Display", _display];

_display ctrlCreate ["ctrlStaticBackgroundDisable", -1];
_display ctrlCreate ["ctrlStaticBackgroundDisableTiles", -1];

// Title
private _title = _display ctrlCreate ["ctrlStaticTitle", -1];
_title ctrlSetPosition
[
    CENTER_X - 0.5 * 80 * GRID_W,
    CENTER_Y - 60 * GRID_H,
    80 * GRID_W,
    5 * GRID_H
];

_title ctrlSetText "Server Settings";

// Background
private _bg = _display ctrlCreate ["ctrlStaticBackground", -1];
_bg ctrlSetPosition
[
    CENTER_X - 0.5 * 80 * GRID_W,
    CENTER_Y - 55 * GRID_H,
    80 * GRID_W,
    26 * GRID_H
];

// Footer
private _footer = _display ctrlCreate ["ctrlStaticFooter", -1];
_footer ctrlSetPosition
[
    CENTER_X - 0.5 * 80 * GRID_W,
    CENTER_Y - 36 * GRID_H,
    80 * GRID_W,
    7 * GRID_H
];

// Host label
private _ipLabel = _display ctrlCreate ["ctrlStatic", -1];
_ipLabel ctrlSetPosition
[
    CENTER_X - 0.5 * 80 * GRID_W,
    CENTER_Y - 55 * GRID_H,
    35 * GRID_W,
    7 * GRID_H
];
_ipLabel ctrlSetText "Host:";

// Host edit
private _lastIp = profileNamespace getVariable ["EOEX_fnc_lastConnect", "127.0.0.1"];
_hostEdit = _display ctrlCreate ["ctrlEdit", -1];
_hostEdit ctrlSetPosition
[
    CENTER_X + 0.5 * 80 * GRID_W - (36 + 1) * GRID_W,
    CENTER_Y - (55 - 1) * GRID_H,
    36 * GRID_W,
    5 * GRID_H
];
_hostEdit ctrlSetText _lastIp;

// Port label
private _portLabel = _display ctrlCreate ["ctrlStatic", -1];
_portLabel ctrlSetPosition
[
    CENTER_X - 0.5 * 80 * GRID_W,
    CENTER_Y - (55 - 6) * GRID_H,
    35 * GRID_W,
    7 * GRID_H
];
_portLabel ctrlSetText "Port:";

// Port edit
private _lastPort = profileNamespace getVariable ["EOEX_fnc_lastPort", "2302"];
_portEdit = _display ctrlCreate ["ctrlEdit", -1];
_portEdit ctrlSetPosition
[
    CENTER_X + 0.5 * 80 * GRID_W - (36 + 1) * GRID_W,
    CENTER_Y - (55 - 1 - 6) * GRID_H,
    36 * GRID_W,
    5 * GRID_H
];
_portEdit ctrlSetText _lastPort;

// Password label
private _passLabel = _display ctrlCreate ["ctrlStatic", -1];
_passLabel ctrlSetPosition
[
    CENTER_X - 0.5 * 80 * GRID_W,
    CENTER_Y - (55 - 6 - 6) * GRID_H,
    35 * GRID_W,
    7 * GRID_H
];
_passLabel ctrlSetText "Password:";

// Password edit
private _passEdit = _display ctrlCreate ["ctrlEdit", -1];
_passEdit ctrlSetPosition
[
    CENTER_X + 0.5 * 80 * GRID_W - (36 + 1) * GRID_W,
    CENTER_Y - (55 - 1 - 6 - 6) * GRID_H,
    36 * GRID_W,
    5 * GRID_H
];

// Connect button
private _connectBtn = _display ctrlCreate ["ctrlButton", -1];
_connectBtn ctrlSetPosition
[
    CENTER_X - 0.5 * 25 * GRID_W,
    CENTER_Y - 35 * GRID_H,
    25 * GRID_W,
    5 * GRID_H
];
_connectBtn ctrlSetText "Connect";

// Host button
private _hostBtn = _display ctrlCreate ["ctrlButton", -1];
_hostBtn ctrlSetPosition
[
    CENTER_X - 0.5 * 80 * GRID_W + 1 * GRID_W,
    CENTER_Y - 35 * GRID_H,
    25 * GRID_W,
    5 * GRID_H
];
_hostBtn ctrlSetText "Host Server";

// Cancel button
private _cancelBtn = _display ctrlCreate ["ctrlButtonCancel", 1];
_cancelBtn ctrlSetPosition
[
    CENTER_X + 0.5 * 80 * GRID_W - (26 * GRID_W),
    CENTER_Y - 35 * GRID_H,
    25 * GRID_W,
    5 * GRID_H
];
_cancelBtn ctrlSetText "Cancel";

uiNamespace setVariable ["EOEX_ConnectDialog_HostEdit", _hostEdit];
uiNamespace setVariable ["EOEX_ConnectDialog_PortEdit", _portEdit];
uiNamespace setVariable ["EOEX_ConnectDialog_PassEdit", _passEdit];

// Button actions
_connectBtn ctrlAddEventHandler ["ButtonClick", {
    if (isNil "EOEX_fnc_initExtension") exitWith {
        [(format ["%1", "EdenOnline EOEX_fnc_initExtension function not found!"]), 1, 5] call BIS_fnc_3DENNotification;
    };

    private _initialized = call EOEX_fnc_initExtension;
    if !(_initialized) exitWith {
        [(format ["%1", "EdenOnline extension failed to initialize. Please check the RPT log for details."]), 1, 5] call BIS_fnc_3DENNotification;
    };

    private _hostCtrl = uiNamespace getVariable "EOEX_ConnectDialog_HostEdit";
    private _portCtrl = uiNamespace getVariable "EOEX_ConnectDialog_PortEdit";
    private _passCtrl = uiNamespace getVariable "EOEX_ConnectDialog_PassEdit";

    private _host = ctrlText _hostCtrl;
    private _port = parseNumber (ctrlText _portCtrl);
    private _password = ctrlText _passCtrl;

    [_host, _port, _password] spawn EOEX_fnc_connect;

    profileNamespace setVariable ["EOEX_fnc_lastConnect", _host];
    profileNamespace setVariable ["EOEX_fnc_lastPort", str(_port)];
    saveProfileNamespace;

    (uiNamespace getVariable ["EOEX_ConnectDialog_Display", displayNull]) closeDisplay 0;
}];

_hostBtn ctrlAddEventHandler ["ButtonClick", {
    // Check if EdenOnline extension is initialized. If not initialize. If it fails, exit with error message.
    if (isNil "EOEX_fnc_initExtension") exitWith {
        [(format ["%1", "EdenOnline EOEX_fnc_initExtension function not found!"]), 1, 5] call BIS_fnc_3DENNotification;
    };

    private _initialized = call EOEX_fnc_initExtension;
    if !(_initialized) exitWith {
        [(format ["%1", "EdenOnline extension failed to initialize. Please check the RPT log for details."]), 1, 5] call BIS_fnc_3DENNotification;
    };

    private _portCtrl = uiNamespace getVariable "EOEX_ConnectDialog_PortEdit";
    private _passCtrl = uiNamespace getVariable "EOEX_ConnectDialog_PassEdit";
    private _port = parseNumber (ctrlText _portCtrl);
    private _password = ctrlText _passCtrl;

    [_port, _password] spawn EOEX_fnc_startServer;

    (uiNamespace getVariable ["EOEX_ConnectDialog_Display", displayNull]) closeDisplay 0;
}];

if (missionNamespace getVariable ["EOEX_var_Connected", false]) then {

    allControls _display apply {
        if (ctrlType _x == ctrlType _passEdit) then
        {
            _x ctrlEnable false;
        };
    };
    _connectBtn ctrlShow false;
    _hostBtn ctrlShow false;

    _disconnectBtn = _display ctrlCreate ["ctrlButton", -1];
    _disconnectBtn ctrlSetPosition
    [
        CENTER_X - 0.5 * 25 * GRID_W,
        CENTER_Y - 35 * GRID_H,
        25 * GRID_W,
        5 * GRID_H
    ];
    _disconnectBtn ctrlSetText "Disconnect";

    _disconnectBtn ctrlAddEventHandler ["ButtonClick", {
        private _isHost = missionNamespace getVariable ["EOEX_var_IsHost", false];
        if (_isHost) then {
            [0] spawn EOEX_fnc_disconnect;
        } else {
            [1] spawn EOEX_fnc_disconnect;
        };
        (uiNamespace getVariable ["EOEX_ConnectDialog_Display", displayNull]) closeDisplay 0;
    }];
};

allControls _display apply {_x ctrlCommit 0};

true