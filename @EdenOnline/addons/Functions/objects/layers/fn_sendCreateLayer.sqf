
params ["_id","_layer","_layerId"];

private _attributes = _layer get3DENAttributes "";

_attributes append [["Layer", _layerId]];

// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, "Layer", _attributes], true] spawn EOEX_fnc_callExtensionAsync;
