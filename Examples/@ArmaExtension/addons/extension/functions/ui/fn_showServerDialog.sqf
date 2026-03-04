0 spawn { 
    disableSerialization; 
 
    // Create empty display 
    _display = (if (is3DEN) then {findDisplay 313} else {[] call BIS_fnc_displayMission}) createDisplay "RscDisplayEmpty"; 
 
    // Background 
    _bg = _display ctrlCreate ["RscText", -1]; 
    _bg ctrlSetPosition [0.3, 0.25, 0.42, 0.45]; // bigger 
    _bg ctrlSetBackgroundColor [0,0,0,0.8]; 
    _bg ctrlCommit 0; 
 
    // Title 
    _title = _display ctrlCreate ["RscText", -1]; 
    _title ctrlSetPosition [0.3, 0.27, 0.4, 0.04]; 
    _title ctrlSetText "Server Settings"; 
    _title ctrlSetBackgroundColor [0,0,0,0]; 
    _title ctrlSetFont "PuristaMedium"; 
    _title ctrlCommit 0; 
 
    // Port label 
    _portLabel = _display ctrlCreate ["RscText", -1]; 
    _portLabel ctrlSetPosition [0.32, 0.36, 0.12, 0.04]; 
    _portLabel ctrlSetText "Port:"; 
    _portLabel ctrlSetBackgroundColor [0,0,0,0]; 
    _portLabel ctrlSetFont "PuristaMedium"; 
    _portLabel ctrlCommit 0; 
 
    // Port edit 
    _portEdit = _display ctrlCreate ["RscEdit", 645]; 
    _portEdit ctrlSetPosition [0.45, 0.36, 0.2, 0.05]; 
    _portEdit ctrlSetBackgroundColor [0,0,0,1]; 
    _portEdit ctrlSetText "2302"; 
    _portEdit ctrlSetFont "PuristaMedium"; 
    _portEdit ctrlCommit 0; 
 
    // Password label 
    _passLabel = _display ctrlCreate ["RscText", -1]; 
    _passLabel ctrlSetPosition [0.32, 0.44, 0.12, 0.04]; 
    _passLabel ctrlSetText "Password:"; 
    _passLabel ctrlSetBackgroundColor [0,0,0,0]; 
    _passLabel ctrlSetFont "PuristaMedium"; 
    _passLabel ctrlCommit 0; 
 
    // Password edit 
    _passEdit = _display ctrlCreate ["RscEdit", 646]; 
    _passEdit ctrlSetPosition [0.45, 0.44, 0.2, 0.05]; 
    _passEdit ctrlSetBackgroundColor [0,0,0,1]; 
    _passEdit ctrlSetText ""; 
    _passEdit ctrlSetFont "PuristaMedium"; 
    _passEdit ctrlCommit 0; 
 
    // Connect button 
    _connectBtn = _display ctrlCreate ["RscButton", 647]; 
    _connectBtn ctrlSetPosition [0.32, 0.52, 0.18, 0.06]; 
    _connectBtn ctrlSetText "Connect"; 
    _connectBtn ctrlSetFont "PuristaMedium"; 
    _connectBtn ctrlCommit 0; 
 
    // Host button 
    _hostBtn = _display ctrlCreate ["RscButton", 649]; 
    _hostBtn ctrlSetPosition [0.52, 0.52, 0.18, 0.06]; 
    _hostBtn ctrlSetText "Host Server"; 
    _hostBtn ctrlSetFont "PuristaMedium"; 
    _hostBtn ctrlCommit 0; 
 
    // Cancel button 
    _cancelBtn = _display ctrlCreate ["RscButton", 648]; 
    _cancelBtn ctrlSetPosition [0.42, 0.62, 0.18, 0.06]; 
    _cancelBtn ctrlSetText "Cancel"; 
    _cancelBtn ctrlSetFont "PuristaMedium"; 
    _cancelBtn ctrlCommit 0; 


    
    uiNamespace setVariable ["ServerDialog_PortEdit", _portEdit];
    uiNamespace setVariable ["ServerDialog_PassEdit", _passEdit];

    // Button actions 
    _connectBtn ctrlAddEventHandler ["ButtonClick", { 
        private _portCtrl = uiNamespace getVariable "ServerDialog_PortEdit";
        private _passCtrl = uiNamespace getVariable "ServerDialog_PassEdit";
        private _port = parseNumber (ctrlText _portCtrl);
        private _password = ctrlText _passCtrl;
 
        systemChat format ["Connecting to server... Port: %1 Password: %2", _port, _password]; 

        [_port, _password] spawn EXT_fnc_connect;
 
 
        _display = (if (is3DEN) then {findDisplay 313} else {[] call BIS_fnc_displayMission}) createDisplay "RscDisplayEmpty"; 
 
        _display closeDisplay 0; 
        // Connect server code here 
    }]; 
 
    _hostBtn ctrlAddEventHandler ["ButtonClick", { 
        private _portCtrl = uiNamespace getVariable "ServerDialog_PortEdit";
        private _passCtrl = uiNamespace getVariable "ServerDialog_PassEdit";
        private _port = parseNumber (ctrlText _portCtrl);
        private _password = ctrlText _passCtrl;
 
        systemChat format ["Hosting server...Port: %1 Password: %2", _port, _password];

        [_port, _password] spawn EXT_fnc_startServer;
 
        _display = (if (is3DEN) then {findDisplay 313} else {[] call BIS_fnc_displayMission}) createDisplay "RscDisplayEmpty"; 
 
        _display closeDisplay 0; 
        // Host server code here 
    }]; 
 
    _cancelBtn ctrlAddEventHandler ["ButtonClick", { 
        _display = (if (is3DEN) then {findDisplay 313} else {[] call BIS_fnc_displayMission}) createDisplay "RscDisplayEmpty"; 
 
        _display closeDisplay 0; 
    }]; 
};