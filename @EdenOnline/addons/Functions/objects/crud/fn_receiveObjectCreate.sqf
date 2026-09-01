
// EOEX_fnc_receiveObjectCreate

params ["_id", "_type", "_attributeMap"];

_attributeMap = createHashMapFromArray _attributeMap;

private _object = create3DENEntity [_type, _attributeMap get "ItemClass", _attributeMap get "Position"];

// Set attributes
{
    private _success = _object set3DENAttribute [_x, _y];
    if !(_success) then { diag_log format ["WARNING: INVALID ATTRIBUTES %1, %2, [%3, %4]", _id, _type, _x, _y ]; };
} forEach _attributeMap;



// TODO Fix other object types (Waypoint, Comment)
if (_type == "Object" || _type == "Trigger" || _type == "Logic") exitWith {
    _object setVariable ["EOEX_var_objectID", _id];
};

if (_type == "Marker") exitWith {
    EOEX_var_Markers set [_id, _object];
};
