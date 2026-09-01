// EOEX_fnc_receiveObjectUpdate


params ["_entityId", "_type", "_attributeMap"];

// [objects, groups, triggers, systems/logic, waypoints, markers, layers, comments]



// Markers
if (_type == "Marker") exitWith {
    private _marker = missionNamespace getVariable ["EOEX_var_Markers", createHashMap] get _entityId;
    if (isNil "_marker") exitWith { diag_log format ["ERROR: Marker not found for ID: %1", _entityId] };

    {
        _x params ["_class", "_value"];

        if (isNil "_class" || isNil "_value") then { continue };
        _success = _marker set3DENAttribute [_class, _value];
        if !(_success) then { diag_log format ["ERROR: INVALID ATTRIBUTES for Marker ID: %1", _entityId] };
    } forEach _attributeMap;
};



// Objects, Triggers, Logic
private _typeIndex = _type call EOEX_fnc_getTypeIndex;
if (_type == "Object" || _type == "Trigger" || _type == "Logic") exitWith {
    {
        private _objId = _x getVariable "EOEX_var_objectID";
        if (!isNil "_objId" && _objId == _entityId) exitWith {
            private _object = _x;
            _object setVariable ["EOEX_updateRequested", true];
            {
                _x params ["_class", "_value"];

                if (isNil "_class" || isNil "_value") then { continue };
                _success = _object set3DENAttribute [_class, _value];
                if !(_success) then { diag_log format ["ERROR: INVALID ATTRIBUTES for Object ID: %1", _entityId] };
            } forEach _attributeMap;
        };
    } forEach (all3DENEntities # _typeIndex);
};
