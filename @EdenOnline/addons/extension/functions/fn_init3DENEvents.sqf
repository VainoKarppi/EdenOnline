

diag_log "3DEN Online Events Initialized";

// Used to queue multiple attribute changes into a single array of changes.
if (isNil "EOEX_var_AttributeQueues") then {
    EOEX_var_AttributeQueues = createHashMap;          // object --> [ [property, value], ... ]
    EOEX_var_AttributeTimers  = createHashMap;         // object --> scriptHandle (for terminate)
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

// TODO keeps spamming the message to server sometimes, and cosntantly sending the object update again and again... This might only happen, because of the mirror?
add3DENEventHandler ["OnEditableEntityAdded", {
	params ["_entity"];
	
	_id = _entity getVariable "EOEX_var_objectID";
	if !(isNil "_id") exitWith {};

	[_entity] spawn EOEX_fnc_createObject;
}];

add3DENEventHandler ["OnEditableEntityRemoved", {
	params ["_entity"];

	// FIX UNTIL THIS GETS FIXED BY BI (single object)
	if (_entity isEqualType grpNull) exitWith {
		{
			[_x] call EOEX_fnc_deleteObject;
		} forEach get3DENSelected "object";
	};

	[_entity] call EOEX_fnc_deleteObject;
}];


add3DENEventHandler ["OnEntityAttributeChanged", {
	_this spawn EOEX_fnc_updateObjectAttributes;
}];

// * CONNECTIONS
add3DENEventHandler ["OnConnectingEnd", {
	params ["_class", "_from", "_to"];
}];

// * MISSION SETTINGS
remove3DENEventHandler ["OnMissionAttributeChanged", uiNamespace getVariable ["EOEX_var_OnMissionAttributeChangedId", -1]];

if (missionNamespace getVariable ["EOEX_var_syncMissionAttributes", false]) then {
	
	private _id = add3DENEventHandler ["OnMissionAttributeChanged", {
		params ["_section", "_property"];
		_value = (_section get3DENMissionAttribute _property);

		[_section,_property,_value] call EOEX_fnc_updateMissionAttributes;
		
	}];

	uiNamespace setVariable ["EOEX_var_OnMissionAttributeChangedId", _id];
};
