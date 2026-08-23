// EOEX_fnc_drawCameras

// Initialize the last update time
if (isNil "EOEX_var_networkCameras") then {
    uiNamespace setVariable ["EOEX_var_networkCameras", createHashMap]; // keys = player UID, values = [pos, dir]
};

diag_log "Starting EOEX_fnc_drawCameras";
uiNamespace setVariable ["EOEX_var_lastCameraTick", diag_tickTime];



// ["EOEX_var_GUIDISPLAY", "onEachFrame"] call BIS_fnc_removeStackedEventHandler;

_code = {
    if (isNull (findDisplay 313)) exitWith {};
    if (is3DENPreview) then {continue};

    // =====================================================================
    // Send own camera position to other clients
    // =====================================================================

    private _lastUpdate = uiNamespace getVariable ["EOEX_var_lastCameraTick", diag_tickTime];

    private _updateInterval = uiNamespace getVariable ["EOEX_var_cameraDrawUpdate", 0.2];

    if (diag_tickTime - _lastUpdate > _updateInterval) then {
        uiNamespace setVariable ["EOEX_var_lastCameraTick", diag_tickTime];

        if (missionNamespace getVariable ["EOEX_var_Connected", false]) then {
            private _startPos = getPosATL get3DENCamera;
            private _forwardVec = vectorDir get3DENCamera;

            ["CameraUpdate", [_startPos, _forwardVec], true] spawn EOEX_fnc_callExtensionAsync;
        };
    };

    // =====================================================================
    // Draw other client cameras
    // =====================================================================

    private _drawDistance = 4000;

    private _cameras = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap];


    private _interpStates = uiNamespace getVariable ["EOEX_var_cameraInterp", createHashMap];

    private _now = diag_tickTime;

    {
        private _clientID = _x;
        private _camData = _y;

        // Make sure the received data has the expected structure
        if (!(_camData isEqualType []) || {count _camData < 2}) then {
            continue
        };

        private _rawPos = _camData select 0;
        private _rawDir = _camData select 1;

        if (
            !(_rawPos isEqualType []) ||
            {!(_rawDir isEqualType [])} ||
            {count _rawPos != 3} ||
            {count _rawDir != 3}
        ) then {
            continue
        };

        // =================================================================
        // Get/create interpolation state
        // =================================================================

        private _interp = _interpStates getOrDefault [_clientID, createHashMap];

        private _initialized = _interp getOrDefault ["initialized", false];

        // =================================================================
        // First packet from this client
        // =================================================================

        if (!_initialized) then {

            _interp set ["initialized", true];

            _interp set ["rawPos", _rawPos];
            _interp set ["rawDir", _rawDir];
            _interp set ["rawTime", _now];

            _interp set ["fromPos", _rawPos];
            _interp set ["toPos", _rawPos];

            _interp set ["fromDir", _rawDir];
            _interp set ["toDir", _rawDir];

            _interp set ["start", _now];
            _interp set ["dur", _updateInterval max 0.05];

            _interp set ["curPos", _rawPos];
            _interp set ["curDir", _rawDir];

            _interpStates set [_clientID, _interp];
        };

        // =================================================================
        // Detect new network data
        // =================================================================

        private _lastRawPos = _interp getOrDefault ["rawPos", _rawPos];

        private _lastRawDir = _interp getOrDefault ["rawDir", _rawDir];

        private _lastRawTime = _interp getOrDefault ["rawTime", _now];

        private _positionChanged = ((_rawPos distance _lastRawPos) > 0.001);

        private _directionChanged = ((_rawDir distance _lastRawDir) > 0.001);

        if (_positionChanged || _directionChanged) then {

            // Current rendered position becomes the beginning
            // of the next interpolation segment.
            private _curPos = _interp getOrDefault ["curPos", _lastRawPos];
            private _curDir = _interp getOrDefault ["curDir", _lastRawDir];

            // Actual time between network updates.
            private _dur = _now - _lastRawTime;

            _dur = 0.05 max (_dur min 0.75);

            // New position interpolation
            _interp set ["fromPos", _curPos];
            _interp set ["toPos", _rawPos];

            // New direction interpolation
            _interp set ["fromDir", _curDir];
            _interp set ["toDir", _rawDir];

            // Start interpolation now
            _interp set ["start", _now];
            _interp set ["dur", _dur];

            // Store latest network data
            _interp set ["rawPos", _rawPos];
            _interp set ["rawDir", _rawDir];
            _interp set ["rawTime", _now];
        };

        // =================================================================
        // Calculate interpolation amount
        // =================================================================

        private _start = _interp getOrDefault ["start", _now];
        private _dur = _interp getOrDefault ["dur", (_updateInterval max 0.05)];
        private _fromPos = _interp getOrDefault ["fromPos",_rawPos];
        private _toPos = _interp getOrDefault ["toPos", _rawPos];
        private _fromDir = _interp getOrDefault ["fromDir", _rawDir];
        private _toDir = _interp getOrDefault ["toDir", _rawDir];

        private _t = if (_dur > 0) then {
            (_now - _start) / _dur
        } else {
            1
        };

        // Clamp to 0..1
        _t = 0 max (_t min 1);

        // =================================================================
        // Interpolate position
        // =================================================================

        private _position = [
            (_fromPos select 0) +
            ((_toPos select 0) - (_fromPos select 0)) * _t,

            (_fromPos select 1) +
            ((_toPos select 1) - (_fromPos select 1)) * _t,

            (_fromPos select 2) +
            ((_toPos select 2) - (_fromPos select 2)) * _t
        ];

        // =================================================================
        // Interpolate direction
        // =================================================================

        private _dir = [
            (_fromDir select 0) +
            ((_toDir select 0) - (_fromDir select 0)) * _t,

            (_fromDir select 1) +
            ((_toDir select 1) - (_fromDir select 1)) * _t,

            (_fromDir select 2) +
            ((_toDir select 2) - (_fromDir select 2)) * _t
        ];

        // Normalize direction safely
        private _dirMagnitude = vectorMagnitude _dir;

        if (_dirMagnitude > 0.0001) then {
            _dir = _dir vectorMultiply (1 / _dirMagnitude);
        } else {
            _dir = _fromDir;
        };

        // =================================================================
        // Store current interpolated state
        // =================================================================

        _interp set ["curPos", _position];
        _interp set ["curDir", _dir];
        _interpStates set [_clientID, _interp];

        // =================================================================
        // Drawing
        // =================================================================

        private _name = EOEX_var_OtherClients getOrDefault [_clientID, "Unknown"];

        // Debug offset
        private _drawPosition = _position;

        // Distance check
        private _localCameraPosition = getPosATL get3DENCamera;

        if (_drawPosition distance _localCameraPosition > _drawDistance) then { continue };

        // =================================================================
        // Camera direction line
        // =================================================================

        // Maximum line length
        private _lineLength = 3000;

        private _lineStart = _drawPosition;

        // Calculate unrestricted end point
        private _lineEnd = _drawPosition vectorAdd (_dir vectorMultiply _lineLength);

        // Convert AGL to ASL for intersection testing
        private _lineStartASL = AGLToASL _lineStart;
        private _lineEndASL = AGLToASL _lineEnd;

        // Find the first object/surface hit by the camera ray
        private _hits = lineIntersectsSurfaces [
            _lineStartASL,
            _lineEndASL,
            objNull,
            objNull,
            true,
            1,
            "GEOM",
            "VIEW"
        ];

        // If something blocks the camera direction, stop the line there
        if (count _hits > 0) then {
            private _hit = _hits select 0;

            // Hit position is the first element
            private _hitPosASL = _hit select 0;

            // Convert back to AGL for drawLine3D
            _lineEnd = ASLToAGL _hitPosASL;
        };

        // Draw camera direction line
        drawLine3D [_lineStart, _lineEnd, [1, 0, 0, 3]];

        // =================================================================
        // Camera icon + name
        // =================================================================

        if (!isNil "_name") then {

            private _yawDeg = (
                (_dir select 0) atan2
                (_dir select 1)
            );

            if (_yawDeg < 0) then {
                _yawDeg = _yawDeg + 360;
            };

            private _iconDir = getDir get3DENCamera - _yawDeg;

            drawIcon3D [
                "a3\3den\data\cfg3den\camera\cameraTexture_ca.paa",
                [0, 0, 1, 1],
                _drawPosition,
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

    // Save interpolation states
    uiNamespace setVariable ["EOEX_var_cameraInterp", _interpStates];
};

["EOEX_var_CameraDrawEvent", "onEachFrame", _code] call BIS_fnc_addStackedEventHandler;

// [Control #51,Control #52,Control #46,Control #47,Control #48,Control #49,Control #87,Control #998,Control #2,Control #76,Control #120,Control #1000,Control #1001,Control #1002,Control #1006,Control #1007,Control #1008,Control #10091,Control #-1,Control #1003]

// Draw map markers
// ((findDisplay 313) displayCtrl 51) ctrlRemoveEventHandler ["Draw", EOEX_var_MAPCTRL];
EOEX_var_MAPCTRL = ((findDisplay 313) displayCtrl 51) ctrlAddEventHandler ["Draw", {
    private _mapCtrl = _this select 0;
    
    private _cameras = uiNamespace getVariable ["EOEX_var_networkCameras", createHashMap];
    {
        private _clientID = _x;
        private _camData = _y; // [pos, dir]

        private _name = missionNamespace getVariable ["EOEX_var_OtherClients",[]] get _clientID;
        private _position = _camData select 0;
        private _dir = _camData select 1;

        private _yawDeg = (_dir select 0) atan2 (_dir select 1);
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
