// EOEX_fnc_receiveObjectDelete

/*
["QI56X8K6","Object"] call EOEX_fnc_receiveObjectDelete;
["HK6WCLSR","Logic"] call EOEX_fnc_receiveObjectDelete;
["CXCZIO8E","Trigger"] call EOEX_fnc_receiveObjectDelete;
["KFCJ1X8C","Marker"] call EOEX_fnc_receiveObjectDelete;
["M7TGTYM2","Waypoint"] call EOEX_fnc_receiveObjectDelete;
["QI56X8K4","Object"] call EOEX_fnc_receiveObjectDelete;
["GJRZY90Y","Comment"] call EOEX_fnc_receiveObjectDelete;
["LSZP9FEE","Layer"] call EOEX_fnc_receiveObjectDelete;
*/
params ["_id", "_type"];

private _deleted = switch (_type) do {
    case "Object";
    case "Trigger";
    case "Logic": { EOEX_var_Objects deleteAt _id };
    case "Marker": { EOEX_var_Markers deleteAt _id };
    case "Comment": { EOEX_var_Comments deleteAt _id };
    case "Waypoint": { EOEX_var_Waypoints deleteAt _id };
    case "Layer": { EOEX_var_Layers deleteAt _id };
    default { nil };
};

// deleteAt returns nil for unknown ids, which leaves _deleted undefined
if (!isNil "_deleted") then {
    delete3DENEntities [_deleted];
};