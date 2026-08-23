// EOEX_fnc_init3DENEvents


diag_log "3DEN Online Events Initialized";

// Used to queue multiple attribute changes into a single array of changes.
if (isNil "EOEX_var_AttributeQueues") then {
    EOEX_var_AttributeQueues = createHashMap;          // object --> [ [property, value], ... ]
    EOEX_var_AttributeTimers  = createHashMap;         // object --> scriptHandle (for terminate)
};
if (isNil "EOEX_var_ApplyingRemoteChanges") then { EOEX_var_ApplyingRemoteChanges = false };
if (isNil "EOEX_var_SyncConnections") then { EOEX_var_SyncConnections = [] };
if (isNil "EOEX_var_SyncConnectionKeys") then { EOEX_var_SyncConnectionKeys = createHashMap };

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
removeAll3DENEventHandlers "OnEditableEntityAdded";
add3DENEventHandler ["OnEditableEntityAdded", {
	params ["_entity"];
	if (missionNamespace getVariable ["EOEX_var_ApplyingRemoteChanges", false]) exitWith {};
	
	if (EOEX_var_DEBUG) then {
		diag_log typeName _entity;
		diag_log _entity;
	};

	// Object, Trigger, System
	if (_entity isEqualType objNull) exitWith {
		if (_entity isKindOf "EmptyDetector") then {
			// TRIGGER
			[_entity] spawn EOEX_fnc_createTrigger;
		} else {
			// OBJECT AND SYSTEM
			private _id = _entity getVariable "EOEX_var_objectID";
			if !(isNil "_id") exitWith {};

			if (EOEX_var_DEBUG) then { diag_log "NEW OBJECT CREATED" };

			[_entity] spawn EOEX_fnc_createObject;
		}
	};

	// Group
	if (_entity isEqualType grpNull) exitWith {
		if (EOEX_var_DEBUG) then {
			diag_log _entity;
			diag_log "GROUP CREATED";
		};
	};

	// Marker
	if (_entity isEqualType "") exitWith {
		if (EOEX_var_DEBUG) then {
			diag_log _entity;
			diag_log "MARKER CREATED";
		};

		[_entity] spawn EOEX_fnc_createMarker
	};

	// Waypoint
	if (_entity isEqualType []) exitWith {
		if (EOEX_var_DEBUG) then {
			diag_log _entity;
			diag_log "WAYPOINT CREATED";
		};
	};

	// LAYER OR COMMENT
	if (_entity isEqualType 0) exitWith {
		// TODO Comment runs: updateObjectAttributes for some reason?
		if (EOEX_var_DEBUG) then {
			diag_log _entity;
			diag_log "LAYER OR COMMENT CREATED";
		};
	};


}];

add3DENEventHandler ["OnEditableEntityRemoved", {
	params ["_entity"];
	if (missionNamespace getVariable ["EOEX_var_ApplyingRemoteChanges", false]) exitWith {};

	// FIX UNTIL THIS GETS FIXED BY BI (single object)
	if (_entity isEqualType grpNull) exitWith {
		{
			[_x] call EOEX_fnc_deleteObject;
		} forEach get3DENSelected "object";
	};

	// OBJECT
	if (_entity isEqualType objNull) exitWith {
		[_entity] call EOEX_fnc_deleteObject;
	};
}];


add3DENEventHandler ["OnEntityAttributeChanged", {
	if (missionNamespace getVariable ["EOEX_var_ApplyingRemoteChanges", false]) exitWith {};
	_this spawn EOEX_fnc_updateObjectAttributes;
}];

// * CONNECTIONS
add3DENEventHandler ["OnConnectingEnd", {
    params ["_class", "_from", "_to"];
    if (missionNamespace getVariable ["EOEX_var_ApplyingRemoteChanges", false]) exitWith {};

    if !(isNil "_to") exitWith {
        if (EOEX_var_DEBUG) then { diag_log "[EdenOnline] Dispatching CREATE connection." };

        [_class, _from, _to] spawn EOEX_fnc_createSyncConnection;
    };

    if (EOEX_var_DEBUG) then { diag_log "[EdenOnline] Dispatching REMOVE connection." };

    [_class, _from] spawn EOEX_fnc_removeSyncConnection;
}];


// * MISSION SETTINGS
remove3DENEventHandler ["OnMissionAttributeChanged", uiNamespace getVariable ["EOEX_var_OnMissionAttributeChangedId", -1]];

if (missionNamespace getVariable ["EOEX_var_syncMissionAttributes", false]) then {
	
	private _id = add3DENEventHandler ["OnMissionAttributeChanged", {
		params ["_section", "_property"];

		private _value = _section get3DENMissionAttribute _property;
		private _key = [_section, _property];

		private _skipValue = EOEX_var_SkipAttributeChange getOrDefault [_key, nil];

		// Prevent echo/feedback loop
		if (!isNil "_skipValue" && {_skipValue isEqualTo _value}) then {
			EOEX_var_SkipAttributeChange deleteAt _key;
		} else {
			[_section, _property, _value] call EOEX_fnc_updateMissionAttributes;
		};
	}];

	uiNamespace setVariable ["EOEX_var_OnMissionAttributeChangedId", _id];
};
