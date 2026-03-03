// Get the 3DEN camera object
_cam = get3DENCamera;

// Get position (ASL coordinates as [x, y, z])
_pos = position _cam;  // Or getPosASL _cam for explicit ASL

// Get direction vector ([dx, dy, dz] — forward facing)
_dir = vectorDir _cam;

// Get up vector ([ux, uy, uz] — for tilt/roll orientation)
_up = vectorUp _cam;

// Hint the data (for debugging — copy this to send over network)
_data = [_pos, _dir, _up];
hint format ["Position: %1\nDirection: %2\nUp: %3", _pos, _dir, _up];

// Draw a red line forward from camera (100m long) — add this to visualize
addMissionEventHandler ["Draw3D", {
    _startPos = position get3DENCamera;
    _forwardVec = vectorDir get3DENCamera;
	
    _endPos = _startPos vectorAdd (_forwardVec vectorMultiply 100);  // Scale length as needed
	hint format ["Position: %1\nDirection: %2\nUp: %3", _startPos, _forwardVec, _endPos];
    drawLine3D [_startPos, _endPos, [1, 0, 0, 5]];  // Red color with full alpha
}];