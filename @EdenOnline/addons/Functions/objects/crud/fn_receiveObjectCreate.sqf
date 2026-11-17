
// EOEX_fnc_receiveObjectCreate

/*

["QI56X8K4","Object",[["ItemClass","B_Soldier_GL_F"],["Position",[4636.22,1131.84,0]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["CXCZIO8E","Trigger",[["ItemClass","EmptyDetector"],["Position",[4651.5,1928.26,0]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["HK6WCLSR","Logic",[["ItemClass","ModuleAnimals_F"],["Position",[5581.05,1296.44,0]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["KFCJ1X8C","Marker",[["ItemClass","mil_destroy"],["Position",[4903.78,1605.54,5]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["GJRZY90Y","Comment",[["ItemClass","loc_talk"],["Position",[4328.92,1233.38,5]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["M7TGTYM2","Waypoint",[["EOEX_Owner","QI56X8K4"],["ItemClass","Move"],["Position",[4713.9,1175.86,0]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["LSZP9FEE","Layer",[["Name","Layer 2"],["Transformation",true],["Visibility",true],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["QI56X8K6","Object",[["ItemClass","O_Soldier_GL_F"],["Position",[4536.22,1131.84,0]],["Layer","LSZP9FEE"]]] spawn EOEX_fnc_receiveObjectCreate;
["XH3CC3T9","Object",[["ItemClass","B_APC_Tracked_01_rcws_F"],["Position",[5609.42,1751.76,0]],["Layer",""],["EOEX_IS_EMPTY",true]]] spawn EOEX_fnc_receiveObjectCreate;

*/

diag_log _this;

params ["_id", "_type", "_attributeMap"];

_attributeMap = createHashMapFromArray _attributeMap;

// Before creating the object, store the ID in the map to prevent duplicate creation

private _entity = if (_type == "Layer") then {
    private _parent = EOEX_var_Layers get (_attributeMap deleteAt "Layer");
    if (isNil "_parent") then {_parent = -1};
    _parent add3DENLayer (_attributeMap get "Name");
} else {
    if (_type == "Waypoint") then {
        sleep 0.01; // Give some time for the parent object to be created
        // TODO ADD AS PROPER TIMER (waituntil Owner exists in EOEX_var_Objects)
        _owner = EOEX_var_Objects get (_attributeMap deleteAt "EOEX_Owner");
        (group _owner) create3DENEntity [_type, _attributeMap get "ItemClass", _attributeMap get "Position"];
    } else {
        private _isEmpty = _attributeMap getOrDefault ["EOEX_IS_EMPTY", false];
        diag_log _isEmpty;
        create3DENEntity [_type, _attributeMap get "ItemClass", _attributeMap get "Position", _isEmpty];
    };
};



if (_type == "Object" || _type == "Trigger" || _type == "Logic") then {
    EOEX_var_Objects set [_id, _entity];
};

if (_type == "Marker") then {
    private _markerName = _attributeMap getOrDefault ["MarkerName",""];
    if (_markerName != "") then {
        _entity set3DENAttribute ["MarkerName", _markerName];
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
    // Add skip attribute to prevent feedback loop
    [_id, [_x,_y]] call EOEX_fnc_addSkipAttributeChange;

    // Special custom attributes:
    if (_x == "Layer") then {

        sleep 0.03; // Give Layer some time to be created before trying this.
        // TODO ADD A PROPER TIMER
        private _layer = EOEX_var_Layers get _y;
        if (!isNil "_layer") then {
            private _success = _entity set3DENLayer _layer;
            if (!_success) then { diag_log format ["ERROR: Unable to change objects layer: %1, layer: %2", _entity, _y] };
        };
        continue;
    };

    private _success = _entity set3DENAttribute [_x, _y];
    if !(_success) then { diag_log format ["WARNING: INVALID ATTRIBUTES %1, %2, [%3, %4]", _id, _type, _x, _y ]; };
} forEach _attributeMap;



