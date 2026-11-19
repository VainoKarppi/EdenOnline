
// Layers always send their whole attribute list, so _isCopy needs no special handling here.
params ["_id","_layer","_layerId", ["_isCopy", false]];

private _attributes = _layer get3DENAttributes "";

if (_layerId != "") then {
    _attributes append [["EOEX_Layer", _layerId]];
};

// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, "Layer", _attributes], true] spawn EOEX_fnc_callExtensionAsync;
