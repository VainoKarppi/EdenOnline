#include "\a3\3den\ui\macros.inc"
#include "\a3\ui_f\hpp\definedikcodes.inc"
#include "\a3\ui_f\hpp\definecommongrids.inc"
#include "\a3\ui_f\hpp\defineresincl.inc"
#include "\a3\ui_f\hpp\defineresincldesign.inc"

// Eden Editor IDDs and IDCs as well as controls types and styles and macros
#include "\a3\3den\ui\resincl.inc"

#define TEXT_W 25
#define LINE_H 3.5
#define BTN_H LINE_H
#define BTN_W BTN_H
#define DIALOG_W 42

private _players = missionNamespace getVariable ["EOEX_var_OtherClients", []];

if (!is3DEN || {_players isEqualTo []}) exitWith {false};

private _testClients = [];

for "_clientID" from 10 to 50 do
{
    _testClients append [[_clientID, format ["Test Client %1", _clientID]]];
};

missionNamespace setVariable
[
    "EOEX_var_OtherClients",
    createHashMapFromArray _testClients
];

private _display = findDisplay IDD_DISPLAY3DEN;
_display getVariable ["EOEX_PlayerList_Controls", []] apply {ctrlDelete _x};

// We can't use ctrlStaticTitle as that's moveable and the whole eden interface can be dragged
private _ctrlTitle = _display ctrlCreate ["ctrlStatic", -1];

_ctrlTitle ctrlSetText "Player List";
_ctrlTitle ctrlSetPosition
[
    safezoneX + safezoneW - (60 + DIALOG_W + 1)  * GRID_W,
    CENTER_Y - 68 * GRID_H,
    DIALOG_W * GRID_W,
    5 * GRID_H
];

_ctrlTitle ctrlSetBackgroundColor (getArray (configFile >> "ctrlStaticTitle" >> "colorBackground") apply
{
    _x call BIS_fnc_parseNumberSafe;
});
_ctrlTitle ctrlCommit 0;

private _ctrlBackground = _display ctrlCreate ["ctrlStaticBackground", -1];

_ctrlBackground ctrlSetPosition
[
    safezoneX + safezoneW - (60 + DIALOG_W + 1) * GRID_W,
    CENTER_Y - (68 - 5) * GRID_H,
    DIALOG_W * GRID_W,
    55 * GRID_H
];

_ctrlBackground ctrlCommit 0;

private _ctrlGroup = _display ctrlCreate ["ctrlControlsGroup", -1];
_ctrlGroup ctrlSetPosition ctrlPosition _ctrlBackground;
_ctrlGroup ctrlCommit 0;

private _yPos = GRID_H;

{
    private _playerID = _x;
    private _playerName = _y;

    private _ctrlClientText = _display ctrlCreate ["ctrlStatic", -1, _ctrlGroup];
    private _ctrlButtonKick = _display ctrlCreate ["ctrlButtonPictureKeepAspect", -1, _ctrlGroup];
    private _ctrlButtonTeleport = _display ctrlCreate ["ctrlButtonPictureKeepAspect", -1, _ctrlGroup];
    private _ctrlButtonTrack = _display ctrlCreate ["ctrlButtonPictureKeepAspect", -1, _ctrlGroup];

    _ctrlClientText ctrlSetPosition [GRID_W, _yPos, TEXT_W * GRID_W, LINE_H * GRID_H];
    _ctrlClientText ctrlSetText _playerName;

    _ctrlButtonKick ctrlSetPosition [(TEXT_W + 2) * GRID_W, _yPos, BTN_W * GRID_W, BTN_H * GRID_H];
    _ctrlButtonKick ctrlSetText "a3\ui_f\data\igui\cfg\simpletasks\types\walk_ca.paa";

    _ctrlButtonTeleport ctrlSetPosition [(TEXT_W + 3 + LINE_H) * GRID_W, _yPos, BTN_W * GRID_W, BTN_H * GRID_H];
    _ctrlButtonTeleport ctrlSetText "a3\ui_f_curator\data\logos\arma3_zeus_icon_ca.paa";

    _ctrlButtonTrack ctrlSetPosition [(TEXT_W + 4 + LINE_H + LINE_H) * GRID_W, _yPos, BTN_W * GRID_W, BTN_H * GRID_H];
    _ctrlButtonTrack ctrlSetText "a3\ui_f\data\igui\cfg\simpletasks\types\walk_ca.paa";

    [_ctrlButtonKick, _ctrlButtonTeleport , _ctrlButtonTrack] apply {_x setVariable ["playerID", _playerID]};

    _yPos = _yPos + (LINE_H + 1) * GRID_H;
} forEach _players;

allControls _ctrlGroup apply {_x ctrlCommit 0};

_display setVariable ["EOEX_PlayerList_Controls", [_ctrlBackground, _ctrlGroup, _ctrlTitle]];

true