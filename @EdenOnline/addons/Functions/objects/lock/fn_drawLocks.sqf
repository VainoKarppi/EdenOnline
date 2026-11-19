// EOEX_fnc_drawLocks

// Draws the indicator of every locked entity ("LOCKED by <user> (<mode>)"). The Draw3D handler is
// added once and removed again by fn_unlockEntity when the last lock was released.

if (!isNil "EOEX_var_LocksHandler") exitWith {};

EOEX_var_LocksHandler = addMissionEventHandler ["Draw3D", {
    if (isNil "EOEX_var_Locks" || {(count EOEX_var_Locks) == 0}) exitWith {};

    {
        _y params [["_entity", nil], ["_username", ""], ["_mode", ""]];

        if (isNil "_entity") then { continue };

        // Groups have no position; markers, comments and waypoints are stored as name, index and
        // [group, index], so only objects may be passed to isNull.
        if (_entity isEqualType grpNull) then { continue };
        if (_entity isEqualType objNull && {isNull _entity}) then { continue };

        private _position = (_entity get3DENAttribute "Position") param [0, nil];

        if (isNil "_position") then { continue };

        private _height = 2.5;

        if (_entity isEqualType objNull) then {
            private _boundingBox = boundingBoxReal _entity;

            if (_boundingBox isEqualType [] && {count _boundingBox > 1}) then {
                _height = (((_boundingBox # 1) # 2) max 0) + 2;
            };
        };

        private _color = if (_mode isEqualTo "drag") then { [1, 0.6, 0.1, 1] } else { [1, 0.25, 0.25, 1] };

        drawIcon3D [
            "\A3\ui_f\data\map\markers\military\warning_CA.paa",
            _color,
            _position vectorAdd [0, 0, _height],
            1.6, 1.6, 0,
            format ["LOCKED by %1 (%2)", _username, _mode],
            2,
            0.055,
            "EtelkaMonospaceProBold",
            "center",
            true
        ];
    } forEach EOEX_var_Locks;
}];
