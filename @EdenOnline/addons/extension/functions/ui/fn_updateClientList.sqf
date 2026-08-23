// EOEX_fnc_updateClientList


params ["_otherClients",[]];
// [[id,"name1"],[id,"name2"]]


// Make an independent copy of the previous client list
private _previousClients = missionNamespace getVariable ["EOEX_var_OtherClients", createHashMap];

_previousClients = +_previousClients;

// Create the new client list
private _newClients = createHashMapFromArray _otherClients;

// Detect newly connected clients
{
    private _clientId = _x;

    if !(_clientId in _previousClients) then {
        private _username = _newClients get _clientId;

        diag_log format ["[EXTENSION] Client connected: %1 (%2)", _clientId, _username];
        systemChat format ["Client connected: %1 (%2)", _clientId, _username];
    };
} forEach keys _newClients;

// Detect disconnected clients and remove their cameras
private _networkCameras = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap];

{
    private _clientId = _x;

    if !(_clientId in _newClients) then {
        private _username = _previousClients getOrDefault [_clientId, format ["Client %1", _clientId]];

        _networkCameras deleteAt _clientId;

        diag_log format ["[EXTENSION] Client disconnected: %1 (%2)", _clientId, _username];
        systemChat format ["Client disconnected: %1 (%2)", _clientId, _username];
    };
} forEach keys _previousClients;

// Store the new client list
missionNamespace setVariable ["EOEX_var_OtherClients", _newClients];

uiNamespace setVariable ["EOEX_var_networkCameras", _networkCameras];

// Update client list UI
[] spawn EOEX_fnc_showPlayersDialog;
