
params ["_id", "_waypoint","_layerId"];

// _waypoint params ["_group", "_index"];

EOEX_var_Waypoints set [_id, _waypoint];

private _position = (_waypoint get3DENAttribute "Position") select 0;
private _class = (_waypoint get3DENAttribute "ItemClass") select 0; // Eg. Follow, Hold, Move, etc


// TODO WAITING FOR BI FEATURE Waypoint attached object. Send the object ID of the attached object, so that we can attach it on other clients
// Remove self from attached list, as we don't want to attach the waypoint to itself
private _attached = (get3DENConnections _waypoint) select { (_x isNotEqualTo ["Group", _waypoint # 0]) };


private _owner = leader (_waypoint # 0);
private _ownerId = _owner call EOEX_fnc_getId;

// TODO Verify creation. Delete if needed and retry.
["CreateObject", [_id, "Waypoint", [["EOEX_Owner", _ownerId], ["ItemClass", _class], ["Position", _position], ["Layer", _layerId]]], true] spawn EOEX_fnc_callExtensionAsync;


private _objects = nearestObjects [_position, [], 1, true];
if (_objects isNotEqualTo []) then {
    ["(EdenOnline) WARNING: Waypoints are not supported if attached to objects! (Waiting for BI support)"] call BIS_fnc_error;
};