


params ["_object"];

// When object is removed via UNDO, this event will only run twice. once for group, once for unit
diag_log "OBJECT REMOVED";
diag_log _object;

if !(missionNamespace getVariable ["EOEX_var_Connected",false]) exitWith {};

if (isNull _object || _object in allGroups) exitWith {};

/*
	_object = ((get3DENSelected "")#0#0);
	_id = call EOEX_fnc_getId;
	_attributes = (_object get3DENAttributes ""); 
	["CreateObject", [_id, _attributes], true] call EOEX_fnc_callExtensionAsync;
*/

// Event was triggered by incoming update from another client
if (_object getVariable ["EOEX_updateRequested", false]) exitWith {
	_object setVariable ["EOEX_updateRequested", nil];
};
_object setVariable ["EOEX_updateRequested", nil];


_id = _object call EOEX_fnc_getId;


["RemoveObject", [_id]] spawn EOEX_fnc_callExtensionAsync;

//_object setVariable ["EOEX_var_objectID", nil];
