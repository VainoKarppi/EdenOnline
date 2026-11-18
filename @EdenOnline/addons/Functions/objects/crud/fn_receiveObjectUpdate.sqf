// EOEX_fnc_receiveObjectUpdate

/*
["QI56X8K6","Object",[["Layer",""]]] call EOEX_fnc_receiveObjectUpdate;
["QI56X8K6","Object",[["pitch",0.98],["speaker","Male02PER"],["Name","asdasd"]]] call EOEX_fnc_receiveObjectUpdate;
["3FQ4S1G2","Group",[["Position",[4697.21,1154.58,0]]]] call EOEX_fnc_receiveObjectUpdate;
["QI56X8K6","Object",[["Position",[4471.52,1284.96,0]]]] call EOEX_fnc_receiveObjectUpdate;
["CXCZIO8E","Trigger",[["Size3",[100,100,-1]]]] call EOEX_fnc_receiveObjectUpdate;
["HK6WCLSR","Logic",[["ModuleAnimals_F_Type","Hen_random_F"]]] call EOEX_fnc_receiveObjectUpdate;
["HK6WCLSR","Logic",[["Init","aadddd"],["Name","testtest"]]] call EOEX_fnc_receiveObjectUpdate;
["KFCJ1X8C","Marker",[["ItemClass","mil_dot"],["BaseColor","Default"]]] call EOEX_fnc_receiveObjectUpdate;
["OZX6ABI7","Marker",[["Text","mymarker"],["MarkerName","marker_777"],["BaseColor","Default"]]] call EOEX_fnc_receiveObjectUpdate;
["QI56X8K4","Object",[["Position",[4630.61,1510.41,0]]]] call EOEX_fnc_receiveObjectUpdate;
["QI56X8K6","Object",[["ItemClass","B_Soldier_AR_F"]]] call EOEX_fnc_receiveObjectUpdate; // Recreated
["XH3CC3T9","Object",[["ItemClass","B_Heli_Transport_01_F"]]] call EOEX_fnc_receiveObjectUpdate; // Recreated
["KFCJ1X8C","Marker",[["ItemClass","mil_dot"]]] call EOEX_fnc_receiveObjectUpdate; // Recreated
*/
params ["_entityId", "_type", "_attributeMap"];

// [objects, groups, triggers, systems/logic, waypoints, markers, layers, comments]

// Add to skip list to avoid sending back the same update to the server
{
    [_entityId, _x] call EOEX_fnc_addSkipAttributeChange;
} forEach _attributeMap;



private _variable = switch (_type) do {
    case "Marker":    { "EOEX_var_Markers" };
    case "Layer":     { "EOEX_var_Layers" };
    case "Waypoint":  { "EOEX_var_Waypoints" };
    case "Comment":   { "EOEX_var_Comments" };

    case "Object";
    case "Trigger";
    case "Logic":     { "EOEX_var_Objects" };

    default           { "" };
};

if (_variable isEqualTo "") exitWith {};

private _entity = missionNamespace getVariable [_variable, createHashMap] get _entityId;
if (isNil "_entity") exitWith {
    diag_log format ["ERROR: %1 not found for ID: %2", _type, _entityId];
};

// Eden ignores set3DENAttribute "ItemClass", the entity has to be created again with the new class.
private _classChange = _attributeMap select { (_x param [0, ""]) isEqualTo "ItemClass" };
if (_classChange isNotEqualTo []) then {
    private _newClass = (_classChange select 0) param [1, ""];
    private _currentClass = (_entity get3DENAttribute "ItemClass") param [0, ""];

    if (_newClass != "" && {_currentClass isEqualType ""} && {_currentClass != ""} && {_newClass != _currentClass}) then {
        private _recreated = [_entityId, _type, _newClass] call EOEX_fnc_recreateEntity;

        if (!isNil "_recreated") then {
            _entity = _recreated;
        };
    };

    _attributeMap = _attributeMap select { (_x param [0, ""]) isNotEqualTo "ItemClass" };
};

{
    _x params ["_class", ["_value", nil]];

    if (isNil "_class" || {isNil "_value"}) then { continue };

    // Special custom attributes:
    if (_class == "Layer") then {
        private _layer = EOEX_var_Layers get _value;
        if (!isNil "_layer") then {
            private _success = _entity set3DENLayer _layer;
            if (!_success) then { diag_log format ["ERROR: Unable to change objects layer: %1, layer: %2", _entity, _value] };
        };
        continue;
    };

    private _success = _entity set3DENAttribute [_class, _value];
    if (!_success) then {
        diag_log format ["ERROR: INVALID ATTRIBUTES for %1 ID: %2", _type, _entityId];
    };
} forEach _attributeMap;

