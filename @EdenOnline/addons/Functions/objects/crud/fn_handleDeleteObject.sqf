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

// A multi-select delete, a composition removal or an undo removes many entities at once. Queue
// them and flush shortly after the first one, so the whole operation leaves as a single message
// instead of one per entity.
if (isNil "EOEX_var_DeleteQueue") then {
    EOEX_var_DeleteQueue = [];
    EOEX_var_DeleteTimer = nil;
};

EOEX_var_DeleteQueue pushBack [_id, _type];

if (isNil "EOEX_var_DeleteTimer" || {scriptDone EOEX_var_DeleteTimer}) then {
    EOEX_var_DeleteTimer = [] spawn {
        // Drain until a window passes without a new removal, so nothing that arrives while the
        // message is being sent stays in the queue.
        while { true } do {
            sleep 0.05; // Collect the removals of this operation first.

            private _pairs = EOEX_var_DeleteQueue;
            EOEX_var_DeleteQueue = [];

            if (_pairs isNotEqualTo []) then {
                // Every element of the arguments array becomes one separate callExtension argument,
                // so the pair list is wrapped once to arrive as the method's single argument.
                ["RemoveObjects", [_pairs], true] call EOEX_fnc_callExtensionAsync;
            };

            if (EOEX_var_DeleteQueue isEqualTo []) exitWith {};
        };
    };
};