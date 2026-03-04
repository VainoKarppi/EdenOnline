params ["_object"];


if (_object getVariable ["EXT_objectID",""] != "") exitWith {_object getVariable ["EXT_objectID",""]};

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
_object setVariable ["EXT_objectID",_id];

_id