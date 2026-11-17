params ["_entity"];


// Some Arma bullshittery: When removing comment or layer we get that SOMETIMES duplicated as in array: eg: [2,2] instead of 2
// They also get removed too early
private _isCommentOrLayer = _entity isEqualType 1;
if (_entity isEqualType [] && {count _entity == 2} && {_entity # 0 isEqualType 0} && {_entity # 1 isEqualType 0} && {_entity # 0 == _entity # 1}) then {
    _entity = _entity select 0;
    _isCommentOrLayer = true;
};

private _type = [_entity, typeName _entity] call EOEX_fnc_getObjectType;

private _id = nil;
if (_isCommentOrLayer && isNil "_type") then {
    {
        if (_y isEqualTo _entity) exitWith { _id = _x; _type = "Layer" };
    } forEach EOEX_var_Layers;
    {
        if (_y isEqualTo _entity) exitWith { _id = _x; _type = "Comment"};
    } forEach EOEX_var_Comments;
};

if (_type == "Group") exitWith {};

// Get id if not Layer or Comment
if (isNil "_id") then { _id = [_entity, true] call EOEX_fnc_getId };
if (isNil "_id") exitWith {}; // This event was a network message to delete object.

// TODO Create batch send, if multiple items deleted at the same time

if (_type == "Object" || _type == "Trigger" || _type == "Logic") then {
    EOEX_var_Objects deleteAt _id;
};

if (_type == "Marker") then {
    EOEX_var_Markers deleteAt _id;
};

if (_type == "Waypoint") then {
    EOEX_var_Waypoints deleteAt _id;
};

if (_type == "Layer") then {
    EOEX_var_Layers deleteAt _id;
};

if (_type == "Comment") then {
    EOEX_var_Comments deleteAt _id;
};

// TODO Verify deletion. Recover if needed and retry.
["RemoveObject", [_id, _type], true] spawn EOEX_fnc_callExtensionAsync;