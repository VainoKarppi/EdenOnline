
// Initialize the last update time
if (isNil "EXT_var_networkCameras") then {
    uiNamespace setVariable ["EXT_var_networkCameras", createHashMap]; // keys = player UID, values = [pos, dir]
};




uiNamespace setVariable ["EXT_var_tickTime", diag_tickTime];






// TODO add smooth interpolation, and read previous history of locations, and calculate "fututre" path
// TODO replace with ctrlAddEventHandler Draw
onEachFrame {
    if (isNull (findDisplay 313)) exitWith {};
    // Dont draw on while preview
    if (is3DENPreview) then {continue};

    // Send this client camera position to other clients via UDP
    _time = uiNamespace getVariable ["EXT_var_tickTime", diag_tickTime];
    if (diag_tickTime - _time > uiNamespace getVariable ["EXT_var_cameraDrawUpdate", 0.1]) then {
        uiNamespace setVariable ["EXT_var_tickTime", diag_tickTime];

        if (missionNamespace getVariable ["EXT_var_Connected", false]) then {
            _startPos = getPosATL get3DENCamera vectorAdd [0,0,-5];
            _forwardVec = vectorDir get3DENCamera;
            ["CameraUpdate", [_startPos, _forwardVec], true] spawn EXT_fnc_callExtensionAsync;
        };
    };

    // Draw other client cameras
    _drawDistance = 2000;
    _cameras = uiNamespace getVariable ["EXT_var_networkCameras", createHashMap];
    {
        _clientID = _x;
        _camData = _y; // [pos, dir]

        _name = EXT_var_OtherClients getOrDefault [_clientID, "Unknown"];
        _position = _camData select 0;
        _dir = _camData select 1;

        // For debug purposes draw mirrored camera from visible angle
        if (EXT_var_DEBUG) then {
            _position vectorAdd [0,0,-5];
        };
        
        // Draw 3d only, if the distance is < _drawDistance
        if (_position distance getPosATL get3DENCamera > _drawDistance) then {continue};

        _end = _position vectorAdd (_dir vectorMultiply 1000);

        drawLine3D [_position, _end, [1,0,0,3]];

        // draw 3d camera icon + name of the other client
        if (!isNil "_name") then { // Should be never nil
            _yawDeg = (_dir select 0) atan2 (_dir select 1);
            if (_yawDeg < 0) then { _yawDeg = _yawDeg + 360 };
            _iconDir = getDir get3DENCamera - _yawDeg;

            drawIcon3D [
                "a3\3den\data\cfg3den\camera\cameraTexture_ca.paa",
                [0, 0, 1, 1],
                _position,
                1.5,
                1.5,
                _iconDir,
                _name,
                0,
                0.03,
                "PuristaMedium",
                "center",
                true
            ];
        };
    } forEach _cameras;
};

// Draw map markers
EXT_var_MAPCTRL = ((findDisplay 313) displayCtrl 51) ctrlAddEventHandler ["Draw", {
    _mapCtrl = _this select 0;
    
    _cameras = uiNamespace getVariable ["EXT_var_networkCameras", createHashMap];
    {
        _clientID = _x;
        _camData = _y; // [pos, dir]

        _name = missionNamespace getVariable ["EXT_var_OtherClients",[]] get _clientID;
        _position = _camData select 0;
        _dir = _camData select 1;

        _yawDeg = (_dir select 0) atan2 (_dir select 1);
        if (_yawDeg < 0) then { _yawDeg = _yawDeg + 360 };

        _mapCtrl drawIcon [
            "a3\3den\data\cfg3den\camera\cameraTexture_ca.paa",
            [1,0,0,1],
            _position,
            24,
            24,
            _yawDeg,
            _name,
            1,
            0.05,
            "TahomaB",
            "right"
        ];
    } forEach _cameras;
}];


/*
onEachFrame { 
    _objects = (get3DENSelected "object"); 
    { 
        _object = _x; 
        if (!isNil "_object") then { 
            _position = getPosAtl _object vectorAdd [0,0,2]; 
            _dir = (vectorDir _object); 
            _end = _position vectorAdd (_dir vectorMultiply 1000); 

            _yawDeg = (_dir select 0) atan2 (_dir select 1); 
            if (_yawDeg < 0) then { _yawDeg = _yawDeg + 360 }; 

            _iconDir = getDir get3DENCamera - _yawDeg; 

            drawIcon3D [ 
                "a3\3den\data\cfg3den\camera\cameraTexture_ca.paa", 
                [0, 0, 1, 1], 
                _position, 
                2, 
                2, 
                _iconDir, 
                "test", 
                0, 
                0.05, 
                "PuristaMedium", 
                "center", 
                true 
            ]; 
        }; 
    } forEach _objects; 
};
*/
/*
onEachFrame {
    _objects = (get3DENSelected "object");
    {
        _object = _x;
        if (!isNil "_object") then {
            _position = getPosAtl _object vectorAdd [0,0,2];
            _dir = (vectorDir _object);
            _end = _position vectorAdd (_dir vectorMultiply 1000);
            _yawDeg = (_dir select 0) atan2 (_dir select 1);
            if (_yawDeg < 0) then { _yawDeg = _yawDeg + 360 };
            _iconDir = getDir get3DENCamera - _yawDeg;


            drawIcon3D [
                "a3\3den\data\cfg3den\camera\cameraTexture_ca.paa",
                [0, 0, 1, 1],
                _position,
                2,
                2,
                _iconDir,
                "test",
                0,
                0.05,
                "PuristaMedium",
                "center",
                true
            ];
        };
    } forEach _objects;
};
*/