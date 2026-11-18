// EOEX_fnc_receiveDragUpdate

params ["_moveId", "_delta"];

if !(missionNamespace getVariable ["EOEX_var_RemoteMoveActive", false]) exitWith {};

private _activeMoveId = missionNamespace getVariable ["EOEX_var_RemoteMoveId", -1];

if (_moveId isNotEqualTo _activeMoveId) exitWith {};

private _moved = missionNamespace getVariable ["EOEX_var_RemoteMoveObjects", []];

// [_id, _entity, _initialPosition] -> [_entity, _targetPosition]
private _targets = _moved apply { [_x # 1, (_x # 2) vectorAdd _delta] };

missionNamespace setVariable ["EOEX_var_RemoteMoveTargets", _targets];
