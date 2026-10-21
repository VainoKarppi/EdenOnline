

params ["_object"];

if (!isNil "_object" && {_object isEqualType objNull && {_object getVariable ["EOEX_var_objectID",""] != ""}}) exitWith {
    true;
};

// Existing marker
if (!isNil "_object" && {_object isEqualType "" && {!isNil "EOEX_var_Markers"}}) then {
    private _id = ""; 
    { 
        _id = _x; 
        if (_y == _object) exitWith {_id};
        _id = "";
    } forEach EOEX_var_Markers; 
    if (_id != "") exitWith { true; };
};

false
