// EOEX_fnc_togglePlayButtons
params [["_enable", false, [true]]];

disableSerialization;

private _display = findDisplay 313;

if (isNull _display) exitWith {
    diag_log "[EdenOnline] Display 313 not found.";
};

private _ctrl = _display displayCtrl 1023;
private _menu = _display displayCtrl 120;

if (isNull _ctrl) exitWith {
    diag_log "[EdenOnline] Control 1023 not found.";
};

if (isNull _menu) exitWith {
    diag_log "[EdenOnline] MenuStrip 120 not found.";
};


// Enable/disable preview control
_ctrl ctrlEnable _enable;


// Tooltip for control 1023
_ctrl ctrlSetTooltip (["You cannot preview the mission while connected to EdenOnline.", ""] select (_enable));


// Enable/disable Play menu
_menu menuEnable [[6], _enable];


// Remove existing tooltip monitor if one exists
private _oldHandle = _display getVariable ["EdenOnline_PlayTooltipHandle", scriptNull];

if (!isNull _oldHandle) then {
    terminate _oldHandle;
    _display setVariable ["EdenOnline_PlayTooltipHandle", scriptNull];
};


// If preview is enabled, we're done
if (_enable) exitWith {
    diag_log "[EdenOnline] Mission preview enabled.";
};


// Create tooltip for Play menu
private _tooltip = _display ctrlCreate ["RscStructuredText", -1];

_tooltip ctrlSetPosition [0,0,0.40,0.06];
_tooltip ctrlSetBackgroundColor [0,0,0,0.9];
_tooltip ctrlSetStructuredText parseText "<t size='0.8' color='#FFFFFF'>You cannot preview the mission while connected to EdenOnline.</t>";

_tooltip ctrlCommit 0;
_tooltip ctrlShow false;

// Don't let the tooltip capture mouse input
_tooltip ctrlEnable false;


// Store tooltip so it can be cleaned up
_display setVariable ["EdenOnline_PlayTooltip", _tooltip];


private _handle = [_display, _menu, _tooltip] spawn {
    params ["_display", "_menu", "_tooltip"];

    while {!isNull _display} do {
        private _hover = menuHover _menu;

        if (_hover isEqualTo [6]) then {
            private _mouse = getMousePosition;

            _tooltip ctrlSetPosition [
                (_mouse # 0) + 0.01,
                (_mouse # 1) + 0.025,
                0.40,
                0.06
            ];

            _tooltip ctrlCommit 0;
            _tooltip ctrlShow true;
        } else {
            _tooltip ctrlShow false;
        };

        uiSleep 0.05;
    };
};


// Store script handle
_display setVariable ["EdenOnline_PlayTooltipHandle", _handle];
