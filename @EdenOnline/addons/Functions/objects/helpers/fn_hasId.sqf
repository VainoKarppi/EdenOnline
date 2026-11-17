// EOEX_fnc_hasId
params ["_object"];

if (isNil "_object") exitWith {
    ["_object undefined as a parameter!"] call bis_fnc_error;
    false
};

private _type = [_object, typeName _object] call EOEX_fnc_getObjectType;

if (isNil "_type") exitWith { false };

private _foundId = "";

switch (_type) do {
    case "Object";
    case "Trigger";
    case "Logic": {
        {
            if (_y isEqualTo _object) exitWith {
                _foundId = _x;
            };
        } forEach EOEX_var_Objects;
    };

    case "Marker": {
        {
            if (_y isEqualTo _object) exitWith {
                _foundId = _x;
            };
        } forEach EOEX_var_Markers;
    };

    case "Comment": {
        {
            if (_y isEqualTo _object) exitWith {
                _foundId = _x;
            };
        } forEach EOEX_var_Comments;
    };

    case "Waypoint": {
        {
            if (_y isEqualTo _object) exitWith {
                _foundId = _x;
            };
        } forEach EOEX_var_Waypoints;
    };

    case "Layer": {
        {
            if (_y isEqualTo _object) exitWith {
                _foundId = _x;
            };
        } forEach EOEX_var_Layers;
    };
};

_foundId != ""