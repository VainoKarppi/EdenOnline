
// EOEX_fnc_receiveObjectCreate

/*

["QI56X8K4","Object",[["ItemClass","B_Soldier_GL_F"],["Position",[4636.22,1131.84,0]],["EOEX_Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["CXCZIO8E","Trigger",[["ItemClass","EmptyDetector"],["Position",[4651.5,1928.26,0]],["EOEX_Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["HK6WCLSR","Logic",[["ItemClass","ModuleAnimals_F"],["Position",[5581.05,1296.44,0]],["EOEX_Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["KFCJ1X8C","Marker",[["ItemClass","mil_destroy"],["Position",[4903.78,1605.54,5]],["EOEX_Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["GJRZY90Y","Comment",[["ItemClass","loc_talk"],["Position",[4328.92,1233.38,5]],["EOEX_Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["M7TGTYM2","Waypoint",[["EOEX_Owner","QI56X8K4"],["ItemClass","Move"],["Position",[4713.9,1175.86,0]],["EOEX_Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["LSZP9FEE","Layer",[["Name","Layer 2"],["Transformation",true],["Visibility",true],["EOEX_Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["QI56X8K6","Object",[["ItemClass","O_Soldier_GL_F"],["Position",[4536.22,1131.84,0]],["EOEX_Layer","LSZP9FEE"]]] spawn EOEX_fnc_receiveObjectCreate;
["XH3CC3T9","Object",[["ItemClass","B_APC_Tracked_01_rcws_F"],["Position",[5609.42,1751.76,0]],["EOEX_Layer",""],["EOEX_IS_EMPTY",true]]] spawn EOEX_fnc_receiveObjectCreate;
["QYZ4ZR3G","Object",[["ItemClass","B_AFV_Wheeled_01_cannon_F"],["Position",[4588.52,6902.68,0]],["EOEX_Layer",""],["EOEX_IS_EMPTY",false]]] spawn EOEX_fnc_receiveObjectCreate;
["2QIONM7S","Object",[["ItemClass","B_Deck_Crew_F"],["Position",[4609.82,6903.2,0]],["EOEX_Layer","KJG2E3NU"],["EOEX_IS_EMPTY",true],["EOEX_GROUP","P6CO3MJ7"]]] spawn EOEX_fnc_receiveObjectCreate;
*/

params ["_id", "_type", "_attributeMap"];

_attributeMap = createHashMapFromArray _attributeMap;

// Before creating the object, store the ID in the map to prevent duplicate creation

private _entity = if (_type == "Layer") then {
    // No EOEX_Layer means the layer is created at the root (-1).
    private _parentId = _attributeMap getOrDefault ["EOEX_Layer", ""];
    _attributeMap deleteAt "EOEX_Layer";
    private _parent = if (_parentId isEqualTo "") then { -1 } else { EOEX_var_Layers getOrDefault [_parentId, -1] };

    private _return = nil;
    ignore3DENHistory {
        _return = _parent add3DENLayer (_attributeMap get "Name");
    };
    _return
} else {
    if (_type == "Waypoint") then {
        // The owning unit may still be in flight from the server, wait until it exists.
        // A batch is applied in order, so the owner is normally already there.
        private _ownerId = _attributeMap deleteAt "EOEX_Owner";
        private _owner = EOEX_var_Objects getOrDefault [_ownerId, objNull];

        if (isNull _owner) then {
            private _deadline = diag_tickTime + 5;
            waitUntil {
                sleep 0.05;
                _owner = EOEX_var_Objects getOrDefault [_ownerId, objNull];
                (!isNull _owner) || {diag_tickTime > _deadline}
            };
        };

        if (isNull _owner) then {
            diag_log format ["ERROR: Unable to create waypoint %1, owner %2 was not found", _id, _ownerId];
            nil
        } else {
            if (isNil {_attributeMap get "ItemClass"} || {isNil {_attributeMap get "Position"}}) then {
                diag_log format ["ERROR: Missing ItemClass or Position for %1 (%2)", _type, _id];
                nil
            } else {
                private _return = nil;
                ignore3DENHistory {
                    _return = (group _owner) create3DENEntity [_type, _attributeMap get "ItemClass", _attributeMap get "Position"];
                };
                _return
            };
        };
    } else {
        private _isEmpty = _attributeMap getOrDefault ["EOEX_IS_EMPTY", false];
        _attributeMap deleteAt "EOEX_IS_EMPTY";

        // A create without ItemClass/Position (e.g. the sender lost the entity while it was reading
        // the attributes) must not throw inside create3DENEntity.
        if (isNil {_attributeMap get "ItemClass"} || {isNil {_attributeMap get "Position"}}) then {
            diag_log format ["ERROR: Missing ItemClass or Position for %1 (%2)", _type, _id];
            nil
        } else {
            private _return = nil;
            ignore3DENHistory {
                _return = create3DENEntity [_type, _attributeMap get "ItemClass", _attributeMap get "Position", _isEmpty];
            };
            _return
        };
    };
};

if (isNil "_entity") exitWith {
    diag_log format ["ERROR: Unable to create %1 (%2)", _type, _id];
};

// Eden groups units placed within 5 m of each other. create3DENEntity always makes a new group,
// so join the one the sender used. The leader may still be in flight, wait for it briefly.
// This runs before the ID is registered: joining a group raises OnEntityParentChanged, and
// EOEX_fnc_updateObjectLayer must not find an ID and send that parent change back to the server.
private _groupId = _attributeMap getOrDefault ["EOEX_GROUP", ""];

if (_groupId != "" && {_type == "Object"}) then {
    _attributeMap deleteAt "EOEX_GROUP";

    private _leader = EOEX_var_Objects getOrDefault [_groupId, objNull];

    if (isNull _leader) then {
        private _deadline = diag_tickTime + 2;
        waitUntil {
            sleep 0.05;
            _leader = EOEX_var_Objects getOrDefault [_groupId, objNull];
            (!isNull _leader) || {diag_tickTime > _deadline}
        };
    };

    if (isNull _leader) then {
        diag_log format ["WARNING: Group leader %1 not found for %2 (%3)", _groupId, _id, _type];
    } else {
        if ((group _entity) isNotEqualTo (group _leader)) then {
            ignore3DENHistory {
                add3DENConnection ["Group", [_entity], group _leader];
            };
        };
    };
};

if (_type == "Object" || _type == "Trigger" || _type == "Logic") then {
    EOEX_var_Objects set [_id, _entity];
};

if (_type == "Marker") then {
    private _markerName = _attributeMap getOrDefault ["MarkerName",""];
    if (_markerName != "") then {
        ignore3DENHistory {
            _entity set3DENAttribute ["MarkerName", _markerName];
        };
        _entity = _markerName;
    };
    EOEX_var_Markers set [_id, _entity];
};

if (_type == "Comment") then {
    EOEX_var_Comments set [_id, _entity];
};

if (_type == "Waypoint") then {
    EOEX_var_Waypoints set [_id, _entity];
};

if (_type == "Layer") then {
    EOEX_var_Layers set [_id, _entity];
};

// Set attributes
{
    // "Order" is the waypoint's slot inside its group: applying it moves the waypoint there and
    // takes over the class stored in that slot. The new waypoint belongs at the end of the list.
    if (_type == "Waypoint" && {_x == "Order"}) then { continue };

    // EOEX custom keys are bookkeeping for the create above, not Eden attributes.
    if (_x select [0,5] == "EOEX_") then {
        if (_x == "EOEX_Layer") then {
            // Add skip attribute to prevent feedback loop
            [_id, [_x,_y]] call EOEX_fnc_addSkipAttributeChange;

            // "" means the entity is not in a custom layer, nothing to do.
            if (_y != "") then {
                // A composition sends its layers before the entities that use them, and a batch is
                // applied in order, so the layer is normally already registered. Only wait when it
                // is still in flight; sleeping unconditionally made every entity in a large paste
                // 30 ms slower and serialized the whole batch.
                private _layer = EOEX_var_Layers get _y;

                if (isNil "_layer") then {
                    sleep 0.03;
                    _layer = EOEX_var_Layers get _y;
                };

                if (!isNil "_layer") then {
                    private _success = false;
                    ignore3DENHistory {
                        _success = _entity set3DENLayer _layer;
                    };
                    if (!_success) then { diag_log format ["ERROR: Unable to change objects layer: %1, layer: %2", _entity, _y] };
                } else {
                    diag_log format ["WARNING: Unknown layer %1 for %2 (%3)", _y, _id, _type];
                };
            };
        };
        continue;
    };

    // Add skip attribute to prevent feedback loop
    [_id, [_x,_y]] call EOEX_fnc_addSkipAttributeChange;

    private _success = false;
    ignore3DENHistory {
        _success = _entity set3DENAttribute [_x, _y];
    };

    if (!_success) then {
        diag_log format ["WARNING: INVALID ATTRIBUTES %1, %2, [%3, %4]", _id, _type, _x, _y ];
    } else {
        // Eden normalizes some values when it applies them (Rotation ends up as a slightly
        // different float), so the change event does not match the received value. Register what
        // the engine reports as well, otherwise every receive echoes back as an endless update.
        private _applied = _entity get3DENAttribute _x;

        if (!isNil "_applied" && {!(_applied isEqualTo [])}) then {
            private _appliedValue = _applied # 0;

            if (!isNil "_appliedValue" && {!(_appliedValue isEqualTo _y)}) then {
                [_id, [_x, _appliedValue]] call EOEX_fnc_addSkipAttributeChange;
            };
        };
    };
} forEach _attributeMap;



