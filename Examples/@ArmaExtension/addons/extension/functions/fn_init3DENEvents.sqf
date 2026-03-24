

diag_log "3DEN Online Events Initialized";


// * OBJECTS

add3DENEventHandler ["OnEditableEntityAdded", {
	params ["_entity"];
	
	_id = _entity getVariable "EXT_objectID";
	if !(isNil "_id") exitWith {};

	[_entity] spawn EXT_fnc_createObject;
}];

add3DENEventHandler ["OnEditableEntityRemoved", {
	params ["_entity"];

	// FIX UNTIL THIS GETS FIXED (single object)
	if (_entity isEqualType grpNull) exitWith {
		{
			[_x] call EXT_fnc_deleteObject;
		} forEach get3DENSelected "object";
	};

	[_entity] call EXT_fnc_deleteObject;
}];


// TODO add to current edit list, and check for every 10 frame, track position, if last update of position 0.5 seconds ago = last position send update
add3DENEventHandler ["OnEntityDragged", {
	params ["_entity"];
	
	[_entity] spawn EXT_fnc_updateObjectPosition;
}];


// One-time setup - can be called from init.sqf or wherever your mod initializes
if (isNil "EXT_var_AttributeQueues") then {
    EXT_var_AttributeQueues = createHashMap;          // object → [ [property, value], ... ]
    EXT_var_AttributeTimers  = createHashMap;         // object → scriptHandle (for terminate)
};

add3DENEventHandler ["OnEntityAttributeChanged", {
	_this call EXT_fnc_updateObjectAttributes;
}];

// * CONNECTIONS
add3DENEventHandler ["OnConnectingEnd", {
	params ["_class", "_from", "_to"];
}];

// * MISSION SETTINGS
add3DENEventHandler ["OnMissionAttributeChanged", {
	params ["_section", "_property"];
}];