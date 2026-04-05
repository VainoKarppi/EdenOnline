

diag_log "3DEN Online Events Initialized";

// Used to queue multiple attribute changes into a single array of changes.
if (isNil "EXT_var_AttributeQueues") then {
    EXT_var_AttributeQueues = createHashMap;          // object --> [ [property, value], ... ]
    EXT_var_AttributeTimers  = createHashMap;         // object --> scriptHandle (for terminate)
};

// * OBJECTS

// TODO add events for copy / cut / paste / undo / redo

// Runs twice, once for GROUP, once for actual UNIT, when selecting from rightside menu
// If unit is copied and pasted, this event will run ONLY once for GROUP (mssing second run for UNIT)
/*
removeAll3DENEventHandlers "OnEditableEntityAdded";
removeAll3DENEventHandlers "OnEditableEntityRemoved";
add3DENEventHandler ["OnEditableEntityAdded", {
	params ["_object"];
	
	[_object] spawn {
		params ["_object"];

		uiSleep 0.01;
		if !(_object isEqualType objNull) then {
			{
				systemChat str(_x); // UNIT
			} forEach get3DENSelected "object";

		};
	};
}];


// WORKS AS EXPECTED (always twice)
add3DENEventHandler ["OnEditableEntityRemoved", {
	params ["_object"];

	[_object] spawn {
		params ["_object"];

		uiSleep 0.01;
		if !(_object isEqualType objNull) then {
			{
				systemChat str(_x); // UNIT
			} forEach get3DENSelected "object";

		};
	};
}];

add3DENEventHandler ["OnPaste", {
	systemChat str(_this);
}];
*/
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


add3DENEventHandler ["OnEntityAttributeChanged", {
	_this call EXT_fnc_updateObjectAttributes;
}];

// * CONNECTIONS
add3DENEventHandler ["OnConnectingEnd", {
	params ["_class", "_from", "_to"];
}];

// * MISSION SETTINGS
removeAll3DENEventHandlers "OnMissionAttributeChanged";
add3DENEventHandler ["OnMissionAttributeChanged", {
	params ["_section", "_property"];
	_value = (_section get3DENMissionAttribute _property);

	[_section,_property,_value] call EXT_fnc_updateMissionAttributes;
	
}];

