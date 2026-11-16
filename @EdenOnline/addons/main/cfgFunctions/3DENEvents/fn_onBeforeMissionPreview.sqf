private _isConnected = missionNamespace getVariable ["EOEX_var_Connected", true];

if (_isConnected) then
{
    ["Preview is disabled as long as you are connected or hosting as server.", 1, 5] call BIS_fnc_3DENNotification;
};

// This needs to be the last line
// Otherwise return value could be overwritten
// And preview would work
_isConnected
