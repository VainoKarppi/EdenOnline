// EOEX_fnc_receiveObjectDelete

// Receives a batch of removals: [[id, type], ...]

/*
private _removals = [["QI56X8K6","Object"],["HK6WCLSR","Logic"],["CXCZIO8E","Trigger"],["KFCJ1X8C","Marker"],["M7TGTYM2","Waypoint"],["QI56X8K4","Object"],["GJRZY90Y","Comment"],["LSZP9FEE","Layer"]];
[_removals] call EOEX_fnc_receiveObjectDelete;

[["3HJRDC50","Object"],["X3UJ308U","Object"]] call EOEX_fnc_receiveObjectDelete;
*/

private _deleted = [];

{
    _x params [["_id", ""], ["_type", ""]];

    if (_id == "") then { continue };

    private _entity = switch (_type) do {
        case "Object";
        case "Trigger";
        case "Logic": { EOEX_var_Objects deleteAt _id };
        case "Marker": { EOEX_var_Markers deleteAt _id };
        case "Comment": { EOEX_var_Comments deleteAt _id };
        case "Waypoint": { EOEX_var_Waypoints deleteAt _id };
        case "Layer": { EOEX_var_Layers deleteAt _id };
        default { nil };
    };

    // deleteAt returns nil for unknown ids, which leaves _entity undefined
    if (!isNil "_entity") then {
        _deleted pushBack _entity;
    };
} forEach _this;

// Removing the whole batch at once keeps a large removal cheap.
if (_deleted isNotEqualTo []) then {
    delete3DENEntities _deleted;
};