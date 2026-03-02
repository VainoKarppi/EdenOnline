params ["_object"];


if (_object in EOE_var_objects) exitWith {_object getVariable "EOE_objectID"};

/*
    Generates a random ID like: "A9F3K2ZQ"
    Usage: _id = call generateRandomId;
*/

generateRandomId = {
    private _chars = toArray "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private _length = 8; // change length if needed
    private _id = "";

    for "_i" from 1 to _length do {
        _id = _id + toString [_chars select floor random count _chars];
    };

    _id
};

private _id = call generateRandomId;
_object setVariable ["EOE_objectID",_id];

_id