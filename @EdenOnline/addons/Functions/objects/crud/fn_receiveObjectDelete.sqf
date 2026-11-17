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

if (_type == "Object" || _type == "Trigger" || _type == "Logic") exitWith {
    private _deleted = EOEX_var_Objects deleteAt _id;
    delete3DENEntities [_deleted];
};

if (_type == "Marker") exitWith {
    private _deleted = EOEX_var_Markers deleteAt _id;
    delete3DENEntities [_deleted];
};

if (_type == "Comment") exitWith {
    private _deleted = EOEX_var_Comments deleteAt _id;
    delete3DENEntities [_deleted];
};

if (_type == "Waypoint") exitWith {
    private _deleted = EOEX_var_Waypoints deleteAt _id;
    delete3DENEntities [_deleted];
};

if (_type == "Layer") exitWith {
    private _deleted = EOEX_var_Layers deleteAt _id;
    delete3DENEntities [_deleted];
};