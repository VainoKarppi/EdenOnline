
params ["_entity", "_newLayer"];

if (_entity isEqualType grpNull) then {
    _entity = leader _entity;
};

_entityId = [_entity, true] call EOEX_fnc_getId;
if (isNil "_entityId") exitWith {};

private _layerId = [get3DENLayer _entity, true] call EOEX_fnc_getId;
if (isNil "_layerId") then {_layerId = ""};

// This is to prevent feedback loop where received object is sent back to server.
private _skip = [_entityId, ["Layer", _layerId]] call EOEX_fnc_checkSkipAttributeChange;
if (_skip) exitWith { diag_log format ["SKIPPING (%1) INCOMING CHANGE: %2, (%3)", _entityId, _newLayer, _layerId] };

diag_log format ["_entity: %1, _entityId: %2, _newLayer: %3, _layerId: %4", _entity, _entityId, _newLayer, _layerId];

private _type = [_entity, typeName _entity] call EOEX_fnc_getObjectType;

["UpdateObject", [_entityId, _type, [["Layer", _layerId]]], true] call EOEX_fnc_callExtensionAsync;