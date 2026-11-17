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


{
    _x params ["_class", "_value"];

    // TODO If class is ItemClass, then we need to recreate the object!!!
    
    
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

    _success = _entity set3DENAttribute [_class, _value];
    if (!_success) then {
        diag_log format ["ERROR: INVALID ATTRIBUTES for %1 ID: %2", _type, _entityId];
    };
} forEach _attributeMap;



// TODO If the itemclass changes we need to recreate the object locally.
// FOR UNITS / VEHICLES AS WELL


// ["QU5KYNM1","Marker",[["ItemClass","mil_box"],["Position",[5675.63,7513.23,5]],["BaseColor","Default"]]] call EOEX_fnc_receiveObjectUpdate;
_attributeMap = createHashMapFromArray _attributeMap;
_newClass = _attributeMap getOrDefault ["ItemClass",""];
if (_type == "Marker" && _newClass != "") exitWith {
    private _copy = _entity get3DENAttributes "";
    private _copyMap = createHashMapFromArray _copy;

    _copyMap set ["ItemClass", _newClass];

    diag_log _copyMap;

    // Delete old Local marker
    [_entityId, "Marker"] call EOEX_fnc_receiveObjectDelete;
    
    // Create new local marker
    [_entityId, "Marker", _copyMap toArray false] call EOEX_fnc_receiveObjectCreate;
};

