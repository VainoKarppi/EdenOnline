

params ["_id", "_comment", "_layerId", ["_isCopy", false]];

private _class = "loc_talk"; // FIXED
private _position = (_comment get3DENAttribute "Position") select 0;

// A pasted comment keeps its text, size, ... so its full attribute list is sent then.
private _attributeMap = if (_isCopy) then {
    createHashMapFromArray ((_comment get3DENAttributes "") select { !isNil { _x # 1 } })
} else {
    createHashMap
};

_attributeMap set ["ItemClass", _class];
_attributeMap set ["Position", _position];

if (_layerId != "") then {
    _attributeMap set ["EOEX_Layer", _layerId];
};


// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, "Comment", _attributeMap], true] spawn EOEX_fnc_callExtensionAsync;

