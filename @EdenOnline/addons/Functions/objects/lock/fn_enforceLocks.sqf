// EOEX_fnc_enforceLocks

// Pins every entity another client is editing ("attributes" lock) so it cannot be moved locally, and
// keeps those entities out of the selection, because Eden drags the selection.
//
// Dragging is handled by Eden itself and cannot be cancelled, so the position is written back every
// frame. A "drag" lock is not pinned: there the entity follows the movement of the client that owns it.

if (!isNil "EOEX_var_LocksEnforceHandler") exitWith {};

EOEX_var_LocksEnforceHandler = addMissionEventHandler ["EachFrame", {
    if (isNil "EOEX_var_Locks" || {(count EOEX_var_Locks) == 0}) exitWith {};

    private _pinned = [];

    {
        _y params [["_entity", nil], ["_username", ""], ["_mode", ""], ["_position", nil]];

        if (_mode isNotEqualTo "attributes") then { continue };
        if (isNil "_entity" || {isNil "_position"}) then { continue };

        // Groups have no position; markers, comments and waypoints are stored as name, index and
        // [group, index], so only objects may be passed to isNull.
        if (_entity isEqualType grpNull) then { continue };
        if (_entity isEqualType objNull && {isNull _entity}) then { continue };

        private _current = (_entity get3DENAttribute "Position") param [0, nil];

        if (isNil "_current") then { continue };

        _pinned pushBack _entity;

        if (_current distance _position > 0.02) then {
            ignore3DENHistory {
                _entity set3DENAttribute ["Position", _position];
            };
        };
    } forEach EOEX_var_Locks;

    if (_pinned isEqualTo []) exitWith {};

    private _selected = [];

    {
        _selected append (get3DENSelected _x);
    } forEach ["object", "trigger", "logic", "waypoint", "marker", "comment"];

    if (_selected isEqualTo []) exitWith {};

    private _keep = _selected select { !(_x in _pinned) };

    if (count _keep == count _selected) exitWith {};

    set3DENSelected _keep;
}];
