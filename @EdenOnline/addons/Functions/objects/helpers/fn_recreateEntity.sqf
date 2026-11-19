// EOEX_fnc_recreateEntity

// Eden ignores set3DENAttribute "ItemClass" (it returns true but keeps the old class), so an
// entity whose class changed has to be deleted and created again. The attributes are preserved.

params ["_id", "_type", ["_newClass", ""]];

private _registry = switch (_type) do {
    case "Object";
    case "Trigger";
    case "Logic":    { EOEX_var_Objects };
    case "Marker":   { EOEX_var_Markers };
    case "Comment":  { EOEX_var_Comments };
    case "Waypoint": { EOEX_var_Waypoints };
    default          { createHashMap };
};

private _entity = _registry getOrDefault [_id, nil];

if (isNil "_entity") exitWith {
    diag_log format ["ERROR: Cannot recreate %1 (%2): entity not found", _type, _id];
    nil
};

if (_newClass == "" || {_type == "Comment"} || {_type == "Layer"}) exitWith {
    diag_log format ["ERROR: Cannot recreate %1 (%2) as %3", _type, _id, _newClass];
    nil
};

// Waypoints are addressed as [group, index]. Eden does not reuse the freed index, so the recreated
// waypoint gets a different index and the registry is updated to it below.
private _waypointGroup = if (_type == "Waypoint") then { _entity # 0 } else { grpNull };

if (_type == "Waypoint" && {isNull _waypointGroup}) exitWith {
    diag_log format ["ERROR: Cannot recreate waypoint %1 as %2: its group is gone", _id, _newClass];
    nil
};

private _attributes = _entity get3DENAttributes "";

// "Order" is the waypoint's slot inside its group: applying it moves the waypoint there and takes
// over the class stored in that slot, so a recreated waypoint must stay at the end of the list.
if (_type == "Waypoint") then {
    _attributes = _attributes select { (_x param [0, ""]) isNotEqualTo "Order" };
};

private _positionResults = _entity get3DENAttribute "Position";
private _position = if (_positionResults isEqualType [] && {_positionResults isNotEqualTo []}) then { _positionResults # 0 } else { [0, 0, 0] };

// Same rule the sender uses, so the recreated entity is crewed the same way.
private _isEmpty = _type == "Object" && {!(_entity isKindOf "Man")} && {crew _entity isEqualTo []};

// Remove the ID first: the deletion event must not be sent to the server as a real removal.
_registry deleteAt _id;

delete3DENEntities [_entity];
sleep 0.02;

private _newEntity = switch (_type) do {
    case "Object";
    case "Trigger";
    case "Logic": { create3DENEntity [_type, _newClass, _position, _isEmpty] };
    case "Waypoint": { _waypointGroup create3DENEntity ["Waypoint", _newClass, _position] };
    default       { create3DENEntity [_type, _newClass, _position] }; // Marker
};

if (isNil "_newEntity") exitWith {
    diag_log format ["ERROR: Cannot recreate %1 (%2) as %3", _type, _id, _newClass];
    nil
};

_registry set [_id, _newEntity];

// MarkerName is skipped on purpose: renaming the new marker would invalidate the name that pending
// create handlers still hold, and the registry tracks markers by ID anyway.
{
    _x params ["_class", ["_value", nil]];

    if (!isNil "_value" && {_class != "ItemClass"} && {_class != "MarkerName"}) then {
        [_id, [_class, _value]] call EOEX_fnc_addSkipAttributeChange;

        if (_newEntity set3DENAttribute [_class, _value]) then {
            // Eden normalizes some values when it applies them (Rotation ends up as a slightly
            // different float), so register what the engine reports as well.
            private _applied = _newEntity get3DENAttribute _class;

            if (!isNil "_applied" && {_applied isNotEqualTo []}) then {
                private _appliedValue = _applied # 0;

                if (!isNil "_appliedValue" && {_appliedValue isNotEqualTo _value}) then {
                    [_id, [_class, _appliedValue]] call EOEX_fnc_addSkipAttributeChange;
                };
            };
        };
    };
} forEach _attributes;

_newEntity
