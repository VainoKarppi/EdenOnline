// EOEX_fnc_getId
params ["_object"];


if (!isNil "_object" && {_object isEqualType objNull && {_object getVariable ["EOEX_var_objectID",""] != ""}}) exitWith {
    _object getVariable "EOEX_var_objectID";
};

// Existing marker
if (!isNil "_object" && {_object isEqualType "" && {!isNil "EOEX_var_Markers"}}) then {
    private _id = ""; 
    { 
        _id = _x; 
        if (_y == _object) exitWith {_id};
        _id = "";
    } forEach EOEX_var_Markers; 
    if (_id != "") exitWith { breakWith _id };
};


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

private _id = call _generateRandomId;

// Object
if (!isNil "_object" && {_object isEqualType objNull}) exitWith {
    _object setVariable ["EOEX_var_objectID",_id];
    _id
};


// Marker
if (!isNil "_object" && {_object isEqualType ""}) exitWith {
    EOEX_var_Markers set [_id, _object];
    _id
};

_id
