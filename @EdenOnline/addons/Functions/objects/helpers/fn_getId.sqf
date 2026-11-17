// EOEX_fnc_getId


params ["_entity", ["_skipNew", false, [true]]];

if (isNil "_entity") exitWith {
    ["_entity undefined as a parameter!"] call bis_fnc_error;
};

private _type = [_entity, typeName _entity] call EOEX_fnc_getObjectType;

private _foundId = "";

private _registry = switch (_type) do {
    case "Object";
    case "Trigger";
    case "Logic": { EOEX_var_Objects };

    case "Marker": { EOEX_var_Markers };

    case "Comment": { EOEX_var_Comments };

    case "Waypoint": { EOEX_var_Waypoints };

    case "Layer": { EOEX_var_Layers };

    default { [] };
};

{
    if (_y isEqualTo _entity) exitWith { _foundId = _x };
} forEach _registry;

if (_foundId != "") exitWith { _foundId };

if (_skipNew) exitWith {};

/*
    Generates a random ID like: "A9F3K2ZQ"
    Usage: _id = call generateRandomId;
*/

private _generateRandomId = {
    private _chars = toArray "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private _length = 8; // change length if needed
    private _id = "";

    for "_i" from 1 to _length do {
        _id = _id + toString [_chars select floor random count _chars];
    };

    _id
};

private _newId = call _generateRandomId;

// Object
if (_type == "Object" || _type == "Trigger" || _type == "Logic") exitWith {
    EOEX_var_Objects set [_newId, _entity];
    _newId
};


// Marker
if (_type == "Marker") exitWith {
    EOEX_var_Markers set [_newId, _entity];
    _newId
};

// Comment
if (_type == "Comment") exitWith {
    diag_log format ["ADDING COMMENT ID TO EOEX_var_Comments %1", _newId];
    EOEX_var_Comments set [_newId, _entity];
    _newId
};

// Layer
if (_type == "Layer") exitWith {
    EOEX_var_Layers set [_newId, _entity];
    _newId
};

if (_type == "Waypoint") exitWith {
    EOEX_var_Waypoints set [_newId, _entity];
    _newId
};


_newId
