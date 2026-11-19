// EOEX_fnc_sendCreateMarker

params ["_id", "_marker","_layerId", ["_isCopy", false]];

private _class = (_marker get3DENAttribute "ItemClass") select 0; // Eg. "mil_destroy", "mil_ambush" ...
private _position = (_marker get3DENAttribute "Position") select 0;

// A pasted marker keeps its text, colour, size, ... so its full attribute list is sent then.
private _attributeMap = if (_isCopy) then {
    createHashMapFromArray ((_marker get3DENAttributes "") select { !isNil { _x # 1 } })
} else {
    createHashMap
};

_attributeMap set ["ItemClass", _class];
_attributeMap set ["Position", _position];

if (_layerId != "") then {
    _attributeMap set ["EOEX_Layer", _layerId];
};


// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, "Marker", _attributeMap], true] spawn EOEX_fnc_callExtensionAsync;
