// EOEX_fnc_init3DENEvents

// loadFile "\z\eoex\addons\Functions\extension\fn_init3DENEvents.sqf"
diag_log "3DEN Online Events Initialized";

// Used to queue multiple attribute changes into a single array of changes.
if (isNil "EOEX_var_AttributeQueues") then {
    EOEX_var_AttributeQueues = createHashMap;          // object --> [ [property, value], ... ]
    EOEX_var_AttributeTimers  = createHashMap;         // object --> scriptHandle (for terminate)
};

// * OBJECTS

// TODO add events for copy / cut / paste / undo / redo

removeAll3DENEventHandlers "OnEditableEntityAdded";
add3DENEventHandler ["OnEditableEntityAdded", {
	params ["_entity"];
	
	diag_log typeName _entity;
	diag_log _entity;

    private _type = _entity call EOEX_fnc_getObjectType;

	// Object, Trigger, System
	if (_type == "Object" || _type == "Trigger" || _type == "Logic") exitWith {
        
        // Already added as object
        private _id = _entity getVariable "EOEX_var_objectID";
        if !(isNil "_id") exitWith {};

        diag_log "NEW OBJECT CREATED";

        [_entity, _type] spawn EOEX_fnc_sendCreateObject;
	};

	// Group
	if (_type == "Group") exitWith {
        // TODO similiar to SyncConnection, but for groups
		diag_log _entity;
		diag_log "GROUP CREATED";
	};

	// Marker
	if (_type == "Marker") exitWith {
		[_entity] call EOEX_fnc_sendCreateMarker
	};

	// Waypoint
	if (_type == "Waypoint") exitWith {
        _waypoint = +_entity;
        _entity params ["_entity","_index"];

        _type = waypointType _waypoint;


        // TODO
		diag_log _waypoint;
        diag_log _type;
		diag_log "WAYPOINT CREATED";
	};

	// LAYER OR COMMENT
	if (_type == "Layer") exitWith {
		// TODO Comment runs: updateObjectAttributes for some reason?
		diag_log _entity;
		diag_log "LAYER OR COMMENT CREATED";
	};

    if (_type == "Comment") exitWith {
        // TODO Comment runs: updateObjectAttributes for some reason?
        diag_log _entity;
        diag_log "COMMENT CREATED";
    };
}];

removeAll3DENEventHandlers "OnEditableEntityRemoved";
add3DENEventHandler ["OnEditableEntityRemoved", {
	params ["_entity"];

	// FIX UNTIL THIS GETS FIXED BY BI (single object)
	if (_entity isEqualType grpNull) exitWith {
		{
			[_x] call EOEX_fnc_sendDeleteObject;
		} forEach get3DENSelected "object";
	};

	// OBJECT
	if (_entity isEqualType objNull) exitWith {
		[_entity] call EOEX_fnc_sendDeleteObject;
	};

    // MARKER
    if (_entity isEqualType "") exitWith {
        [_entity] call EOEX_fnc_sendDeleteMarker;
    };
}];

removeAll3DENEventHandlers "OnEntityAttributeChanged";
add3DENEventHandler ["OnEntityAttributeChanged", {
	_this spawn EOEX_fnc_sendObjectAttributes;
}];


// * CONNECTIONS
removeAll3DENEventHandlers "OnConnectingEnd";
add3DENEventHandler ["OnConnectingEnd", {
    params ["_class", "_from", "_to"];

    if !(isNil "_to") exitWith {
        diag_log "[EdenOnline] Dispatching CREATE connection.";

        [_class, _from, _to] spawn EOEX_fnc_createSyncConnection;
    };

    diag_log "[EdenOnline] Dispatching REMOVE connection.";

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
			[_section, _property, _value] call EOEX_fnc_sendMissionAttributes;
		};
	}];

	uiNamespace setVariable ["EOEX_var_OnMissionAttributeChangedId", _id];
};

// -----------------------------------------------------------------------------
// OnEntityDragStart
// -----------------------------------------------------------------------------


removeAll3DENEventHandlers "OnEntityDragged";
add3DENEventHandler ["OnEntityDragged", {
    params ["_entity"];

    [_entity] call EOEX_fnc_onEntityDragStart;
}];

// -----------------------------------------------------------------------------
// MouseButtonUp (Release selected objects)
// -----------------------------------------------------------------------------
private _display = findDisplay 313;
if !(isNull _display) then {

	if !(isNil "EOX_var_ObjectReleasedEvent") then {
        _display displayRemoveEventHandler ["MouseButtonUp", EOX_var_ObjectReleasedEvent];
    };

    EOX_var_ObjectReleasedEvent = _display displayAddEventHandler ["MouseButtonUp", {
        params ["_display", "_button"];

        // 0 = left mouse button
        if (_button != 0) exitWith {};

        call EOEX_fnc_onEntityDragEnd;
    }];
};


/*
// -----------------------------------------------------------------------------
// OnEntityDragged
// -----------------------------------------------------------------------------
removeAll3DENEventHandlers "OnEntityDragged";

private _display = findDisplay 313;

missionNamespace setVariable ["Eden_MoveActive", false];
missionNamespace setVariable ["Eden_MoveLastUpdate", 0];
missionNamespace setVariable ["Eden_MoveId", -1];
missionNamespace setVariable ["Eden_MoveEntities", []];
missionNamespace setVariable ["Eden_MoveReferencePosition", [0, 0, 0]];
missionNamespace setVariable ["Eden_MoveInitialPositions", []];
missionNamespace setVariable ["Eden_MoveTargets", []];
missionNamespace setVariable ["Eden_MoveLastUpdate", 0];

// Create test objects

private _object1 = create3DENEntity [
    "Object",
    "B_Soldier_F",
    [6330, 770, 0]
];

private _object2 = create3DENEntity [
    "Object",
    "B_Soldier_F",
    [6338, 762, 0]
];

missionNamespace setVariable [
    "Eden_TestObject1",
    _object1
];

missionNamespace setVariable [
    "Eden_TestObject2",
    _object2
];

// Only select the first object.
// The second object simulates the remote object.
set3DENSelected [_object1];

// Store the initial position of the simulated remote object.

missionNamespace setVariable [
    "Eden_MoveInitialPositions",
    [
        [
            _object1 call EOEX_fnc_getId,
            (_object1 get3DENAttribute "Position") select 0
        ],
        [
            _object2 call EOEX_fnc_getId,
            (_object2 get3DENAttribute "Position") select 0
        ]
    ]
];

// Simulated receiver update loop.
//
// This represents the remote client receiving MOVE_UPDATE over UDP.
// The target position is updated from the cumulative movement delta,
// while the actual object is smoothly interpolated toward the target.

missionNamespace setVariable [
    "Eden_MoveTargets",
    [
        (_object2 get3DENAttribute "Position") select 0
    ]
];

missionNamespace setVariable [
    "Eden_MoveLastUpdate",
    0
];

missionNamespace setVariable [
    "Eden_MoveSimulationActive",
    true
];

addMissionEventHandler ["EachFrame", {
    if !(missionNamespace getVariable [
        "Eden_MoveSimulationActive",
        false
    ]) exitWith {};

    private _targets = missionNamespace getVariable [
        "Eden_MoveTargets",
        []
    ];

    if (_targets isEqualTo []) exitWith {};

    private _object2 = missionNamespace getVariable [
        "Eden_TestObject2",
        objNull
    ];

    if (isNull _object2) exitWith {};

    private _currentPosition =
        (_object2 get3DENAttribute "Position") select 0;

    private _targetPosition = _targets select 0;

    private _delta =
        _targetPosition vectorDiff _currentPosition;

    private _distance = vectorMagnitude _delta;

    if (_distance < 0.001) exitWith {};

    private _lerpSpeed = 12;

    private _factor = 1 - exp (-_lerpSpeed * diag_deltaTime);

    private _newPosition =
        _currentPosition vectorAdd (_delta vectorMultiply _factor);

    _object2 set3DENAttribute [
        "Position",
        _newPosition
    ];
}];

add3DENEventHandler ["OnEntityDragged", {
    params ["_entity"];

    if !(_entity isEqualType objNull) exitWith {};

    private _object1 = missionNamespace getVariable [
        "Eden_TestObject1",
        objNull
    ];

    if (isNull _object1) exitWith {};

    if !(_entity isEqualTo _object1) exitWith {};

    private _now = diag_tickTime;
    private _position =
        (_entity get3DENAttribute "Position") select 0;

    // Start a new movement operation.

    if !(missionNamespace getVariable [
        "Eden_MoveActive",
        false
    ]) then {

        private _moveId = _entity call EOEX_fnc_getId;

        private _entities = get3DENSelected "object";

        if (_entities isEqualTo []) then {
            _entities = [_entity];
        };

        private _objectIds = [];

        {
            _objectIds pushBack (_x call EOEX_fnc_getId);
        } forEach _entities;

        missionNamespace setVariable [
            "Eden_MoveActive",
            true
        ];

        missionNamespace setVariable [
            "Eden_MoveId",
            _moveId
        ];

        missionNamespace setVariable [
            "Eden_MoveEntities",
            _entities
        ];

        missionNamespace setVariable [
            "Eden_MoveReferencePosition",
            _position
        ];

        missionNamespace setVariable [
            "Eden_MoveLastUpdate",
            _now
        ];

        private _message = [
            "START_MOVE",
            _moveId,
            _objectIds
        ];

        diag_log format [
            "START_MOVE: %1",
            _message
        ];

        // TODO: Send START_MOVE via TCP
    };

    private _lastUpdate = missionNamespace getVariable [
        "Eden_MoveLastUpdate",
        0
    ];

    if ((_now - _lastUpdate) < 0.1) exitWith {};

    private _moveId = missionNamespace getVariable [
        "Eden_MoveId",
        -1
    ];

    private _referencePosition = missionNamespace getVariable [
        "Eden_MoveReferencePosition",
        _position
    ];

    // Calculate cumulative movement relative to the position
    // where the drag started.

    private _delta =
        _position vectorDiff _referencePosition;

    private _message = [
        "MOVE_UPDATE",
        _moveId,
        _delta
    ];

    diag_log format [
        "MOVE_UPDATE: %1",
        _message
    ];

    // TODO: Send MOVE_UPDATE via UDP

    // Simulate the remote client receiving this UDP update.
    //
    // The second object's original position is used as the base,
    // so it maintains its relative position to the dragged object.

    private _object2 = missionNamespace getVariable [
        "Eden_TestObject2",
        objNull
    ];

    if !(isNull _object2) then {

        private _initialPositions =
            missionNamespace getVariable [
                "Eden_MoveInitialPositions",
                []
            ];

        private _object2Id =
            _object2 call EOEX_fnc_getId;

        private _initialEntry = [];

        {
            if ((_x select 0) isEqualTo _object2Id) exitWith {
                _initialEntry = _x;
            };
        } forEach _initialPositions;

        if !(_initialEntry isEqualTo []) then {

            private _initialPosition =
                _initialEntry select 1;

            private _targetPosition =
                _initialPosition vectorAdd _delta;

            missionNamespace setVariable [
                "Eden_MoveTargets",
                [_targetPosition]
            ];
        };
    };

    missionNamespace setVariable [
        "Eden_MoveLastUpdate",
        _now
    ];
}];

if !(isNull _display) then {

    _display displayAddEventHandler ["MouseButtonUp", {
        params ["_display", "_button"];

        if (_button != 0) exitWith {};

        if !(missionNamespace getVariable [
            "Eden_MoveActive",
            false
        ]) exitWith {};

        private _moveId = missionNamespace getVariable [
            "Eden_MoveId",
            -1
        ];

        private _entities = missionNamespace getVariable [
            "Eden_MoveEntities",
            []
        ];

        private _finalPositions = [];

        {
            private _objectId = _x call EOEX_fnc_getId;

            private _position =
                (_x get3DENAttribute "Position") select 0;

            _finalPositions pushBack [
                _objectId,
                _position
            ];
        } forEach _entities;

        // Include the simulated remote object in the final state.

        private _object2 = missionNamespace getVariable [
            "Eden_TestObject2",
            objNull
        ];

        if !(isNull _object2) then {

            private _object2Id =
                _object2 call EOEX_fnc_getId;

            private _object2Position =
                (_object2 get3DENAttribute "Position") select 0;

            _finalPositions pushBack [
                _object2Id,
                _object2Position
            ];
        };

        private _message = [
            "END_MOVE",
            _moveId,
            _finalPositions
        ];

        diag_log format [
            "END_MOVE: %1",
            _message
        ];

        // TODO: Send END_MOVE via TCP

        missionNamespace setVariable [
            "Eden_MoveActive",
            false
        ];

        missionNamespace setVariable [
            "Eden_MoveLastUpdate",
            0
        ];

        missionNamespace setVariable [
            "Eden_MoveId",
            -1
        ];

        missionNamespace setVariable [
            "Eden_MoveEntities",
            []
        ];

        missionNamespace setVariable [
            "Eden_MoveReferencePosition",
            [0, 0, 0]
        ];
    }];
};

*/
