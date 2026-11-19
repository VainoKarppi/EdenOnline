// EOEX_fnc_getEntityById

// Resolves an EOEX id through every registry. Markers are stored by name, comments by index and
// waypoints as [group, index], so the returned handle type differs per entity type.

params [["_id", "", [""]]];

if (_id isEqualTo "") exitWith {};

private _entity = nil;

{
    private _found = _x getOrDefault [_id, nil];

    if (!isNil "_found") exitWith {
        _entity = _found;
    };
} forEach [EOEX_var_Objects, EOEX_var_Markers, EOEX_var_Waypoints, EOEX_var_Comments];

_entity
