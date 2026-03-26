

diag_log "3DEN Online Events Initialized";


// * OBJECTS

// TODO add events for copy / cut / paste / undo / redo


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



// Used to queue multiple attribute changes into a single array of changes.
if (isNil "EXT_var_AttributeQueues") then {
    EXT_var_AttributeQueues = createHashMap;          // object --> [ [property, value], ... ]
    EXT_var_AttributeTimers  = createHashMap;         // object --> scriptHandle (for terminate)
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