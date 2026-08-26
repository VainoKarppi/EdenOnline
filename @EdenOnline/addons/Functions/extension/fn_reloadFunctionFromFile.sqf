// EOEX_fnc_reloadFunctionFromFile

// "\z\<TAG>\addons\functions\fn_reloadFunctions.sqf"
// "\z\<TAG>\addons\<addon>\fn_<functionName>.sqf"

params [["_filePath", ""]];

if (_filePath isEqualTo "") exitWith {false};

diag_log _filePath; 


// Get filename from path
private _fileName = _filePath splitString "\\/" select -1;

// Require fn_*.sqf
if !(_fileName select [0, 3] isEqualTo "fn_") exitWith {
    diag_log format ["EOEX RELOAD: Invalid function filename: %1", _filePath];

    false
};

// Remove .sqf
private _functionName = _fileName select [3, (count _fileName) - 7];

// Extract TAG from:
// \z\TAG\addons\...
private _pathParts = _filePath splitString "\\/";

private _zIndex = _pathParts find "z";

if (_zIndex == -1 || {_zIndex + 1 >= count _pathParts}) exitWith {
    diag_log format ["EOEX RELOAD: Could not determine tag from path: %1", _filePath];

    false
};

private _tag = _pathParts select (_zIndex + 1); 

private _variableName = format ["%1_fnc_%2", _tag, _functionName];

missionNamespace setVariable [_variableName, compile preprocessFileLineNumbers _filePath];

diag_log format ["EOEX RELOAD: Reloaded function: %1 from %2", _variableName, _filePath];

true
