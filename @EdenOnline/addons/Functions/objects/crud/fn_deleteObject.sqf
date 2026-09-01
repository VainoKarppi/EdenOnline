// EOEX_fnc_deleteObject



params ["_object"];



if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

if (isNull _object || _object in allGroups) exitWith {};

private _objectId = _object getVariable "EOEX_var_objectID";
if (isNil "_objectId") exitWith {}; // Can be triggered twice

_object setVariable ["EOEX_var_objectID", nil];

// When object is removed via UNDO, this event will only run twice. once for group, once for unit
diag_log "OBJECT REMOVED";
diag_log _object;


// Event was triggered by incoming update from another client
if (_object getVariable ["EOEX_updateRequested", false]) exitWith {
	_object setVariable ["EOEX_updateRequested", nil];
};
_object setVariable ["EOEX_updateRequested", nil];


["RemoveObject", [_objectId]] spawn EOEX_fnc_callExtensionAsync;
