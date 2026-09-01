// EOEX_fnc_receiveObjectDelete


params ["_id"];

if (_id in EOEX_var_Markers) exitWith {
    private _marker = EOEX_var_Markers get _id;
    diag_log format ["DELETE_MARKER: %1, ID: %2", _marker, _id];
    EOEX_var_Markers deleteAt _id;
    deleteMarker _marker;

    // TODO MAYBE ??? PREVENT FEEDBACK LOOP: This will trigger a "EntityRemoved" event, which will call this function again. We need to prevent that from happening.
};

//  all3DENEntities params ["_objects", "_groups", "_triggers", "_systems/logic", "_waypoints", "_markers", "_layers", "_comments"];

{
    private _objects = (all3DENEntities # _x) select {
        _x getVariable ["EOEX_var_objectID", "-1"] == _id
    };

    if (count _objects > 0) exitWith {
        delete3DENEntities _objects;
    };
} forEach [0, 2, 3];