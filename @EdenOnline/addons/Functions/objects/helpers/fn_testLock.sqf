
/*
    EOEX_fnc_testLock

    Debug helper for entity locking.

    Actions:
        "lock"   - lock entity and show indicator
        "unlock" - release lock and remove indicator
        "hide"   - hide indicator, keep lock active
        "show"   - show indicator again

    Parameters:
        _action          "lock", "unlock", "hide" or "show"
        _entity          Entity to lock. Defaults to a newly created building.
        _username        Lock holder name.
        _mode            "attributes" or "drag".
        _deleteOnUnlock  Delete automatically created entity.
        _class           Class of automatically created entity.
    
    Examples:
        private _entity = (get3DENMouseOver) param [1, objNull];

        if (!isNull _entity) then {
            ["lock", _entity, "Razer", "attributes"] call EOEX_fnc_testLock;
            ["lock", _entity, "Razer", "drag"] call EOEX_fnc_testLock;
        } else {
            systemChat "No object under cursor.";
        };
*/

params [
    ["_action", "lock", [""]],
    ["_entity", nil],
    ["_username", "Razer", [""]],
    ["_mode", "attributes", [""]],
    ["_deleteOnUnlock", true, [false]],
    ["_class", "Land_i_House_Small_01_F", [""]]
];

// Remove the Draw3D indicator.
private _removeIndicator = {
    if (!isNil "EOEX_var_TestLockHandler") then {
        removeMissionEventHandler ["Draw3D", EOEX_var_TestLockHandler];
        EOEX_var_TestLockHandler = nil;
    };
};

// Stop lock monitoring and restore icon visibility.
private _stopLockMonitor = {
    if (!isNil "EOEX_var_TestLockMonitor") then {
        removeMissionEventHandler ["EachFrame", EOEX_var_TestLockMonitor];
        EOEX_var_TestLockMonitor = nil;
    };

    if (!isNil "EOEX_var_TestLockDragHandler") then {
        remove3DENEventHandler ["OnEntityDragged", EOEX_var_TestLockDragHandler];
        EOEX_var_TestLockDragHandler = nil;
    };

    if (!isNil "EOEX_var_TestLockAttributeHandler") then {
        remove3DENEventHandler ["OnEntityAttributeChanged", EOEX_var_TestLockAttributeHandler];
        EOEX_var_TestLockAttributeHandler = nil;
    };

    if (missionNamespace getVariable ["EOEX_var_TestLockIconsHidden", false]) then {
        set3DENIconsVisible (missionNamespace getVariable ["EOEX_var_TestLockIcons", [true, true]]);
    };

    EOEX_var_TestLockIconsHidden = false;
};

// Restore the original position and rotation of a locked entity.
private _restoreTransform = {
    params ["_entity"];

    if (isNil "EOEX_var_TestLock") exitWith {};

    EOEX_var_TestLock params [
        "_lockedEntity",
        "",
        "",
        "",
        "_lockedPosition",
        "_lockedRotation"
    ];

    if (_entity isNotEqualTo _lockedEntity) exitWith {};

    private _position = (_entity get3DENAttribute "position") param [0, []];
    private _rotation = (_entity get3DENAttribute "rotation") param [0, []];

    private _restorePosition =
        _lockedPosition isNotEqualTo [] &&
        {_position isNotEqualTo _lockedPosition};

    private _restoreRotation =
        _lockedRotation isNotEqualTo [] &&
        {_rotation isNotEqualTo _lockedRotation};

    if (_restorePosition || _restoreRotation) then {
        ignore3DENHistory {
            if (_restorePosition) then {
                _entity set3DENAttribute ["position", _lockedPosition];
            };

            if (_restoreRotation) then {
                _entity set3DENAttribute ["rotation", _lockedRotation];
            };
        };
    };
};

// Start lock monitoring.
private _startLockMonitor = {
    call _stopLockMonitor;

    EOEX_var_TestLockIcons = get3DENIconsVisible;
    EOEX_var_TestLockIconsHidden = false;

    // Detect dragging and immediately restore the original transform.
    EOEX_var_TestLockDragHandler = add3DENEventHandler ["OnEntityDragged", {
        _this call EOEX_fnc_testLockRestoreTransform;
    }];

    // Detect position/rotation changes from the attributes interface.
    EOEX_var_TestLockAttributeHandler = add3DENEventHandler ["OnEntityAttributeChanged", {
        params ["_entity", "_property"];

        if (toLower _property in ["position", "rotation"]) then {
            [_entity] call EOEX_fnc_testLockRestoreTransform;
        };
    }];

    // Monitor selection and mouse interaction.
    EOEX_var_TestLockMonitor = addMissionEventHandler ["EachFrame", {
        if (isNil "EOEX_var_TestLock") exitWith {};

        private _lockedEntity = EOEX_var_TestLock # 0;

        // Remove locked entities from the current selection.
        private _remaining = [];
        private _lockedSelected = false;

        {
            private _selected = get3DENSelected _x;

            if (_lockedEntity in _selected) then {
                _lockedSelected = true;
            };

            _remaining append (_selected select {
                _x isNotEqualTo _lockedEntity
            });

        } forEach [
            "object",
            "group",
            "trigger",
            "logic",
            "waypoint",
            "marker",
            "comment"
        ];

        if (_lockedSelected) then {
            set3DENSelected _remaining;
        };

        // Check whether the mouse is hovering over the locked entity.
        private _mouseOver = get3DENMouseOver;

        private _hoveringLocked =
            count _mouseOver > 1 &&
            {(_mouseOver # 1) isEqualTo _lockedEntity};

        if (_hoveringLocked) then {
            if !(missionNamespace getVariable ["EOEX_var_TestLockIconsHidden", false]) then {
                set3DENIconsVisible [false, false];
                EOEX_var_TestLockIconsHidden = true;
            };
        } else {
            if (missionNamespace getVariable ["EOEX_var_TestLockIconsHidden", false]) then {
                set3DENIconsVisible (missionNamespace getVariable ["EOEX_var_TestLockIcons", [true, true]]);
                EOEX_var_TestLockIconsHidden = false;
            };
        };
    }];
};

// Draw the lock indicator.
private _showIndicator = {
    call _removeIndicator;

    if (isNil "EOEX_var_TestLock") exitWith {};

    EOEX_var_TestLockHandler = addMissionEventHandler ["Draw3D", {
        if (isNil "EOEX_var_TestLock") exitWith {};

        EOEX_var_TestLock params ["_entity", "_username", "_mode", "_id"];

        // Resolve the ID once synchronization registers it.
        if (_id isEqualTo "unregistered") then {
            private _resolved = [_entity, true] call EOEX_fnc_getId;

            if (!isNil "_resolved" && {_resolved isNotEqualTo ""}) then {
                _id = _resolved;
                EOEX_var_TestLock set [3, _resolved];
            };
        };

        if (_entity isEqualType grpNull) exitWith {};

        private _positionResults = _entity get3DENAttribute "Position";

        private _position = if (
            _positionResults isEqualType [] &&
            {_positionResults isNotEqualTo []}
        ) then {
            _positionResults # 0
        } else {
            nil
        };

        if (isNil "_position") exitWith {};

        private _color = if (toLower _mode == "drag") then {
            [1, 0.6, 0.1, 1]
        } else {
            [1, 0.25, 0.25, 1]
        };

        private _label = format [
            "LOCKED by %1 (%2) - %3",
            _username,
            _mode,
            _id
        ];

        private _height = 2.5;

        if (_entity isEqualType objNull && {!isNull _entity}) then {
            private _boundingBox = boundingBoxReal _entity;

            if (_boundingBox isEqualType [] && {count _boundingBox > 1}) then {
                _height = ((_boundingBox # 1) # 2) max 0;
                _height = _height + 2;
            };
        };

        drawIcon3D [
            "\A3\ui_f\data\map\markers\military\warning_CA.paa",
            _color,
            _position vectorAdd [0, 0, _height],
            1.6,
            1.6,
            0,
            _label,
            2,
            0.055,
            "EtelkaMonospaceProBold",
            "center",
            true
        ];
    }];
};

switch (toLower _action) do {

    case "lock": {
        private _created = false;

        call _removeIndicator;
        call _stopLockMonitor;

        if (isNil "_entity") then {
            private _position = positionCameraToWorld [0, 0, 35];
            _position set [2, 0];

            _entity = create3DENEntity ["Object", _class, _position];
            _created = true;
        };

        if (isNil "_entity") exitWith {};

        private _id = [_entity, true] call EOEX_fnc_getId;

        if (isNil "_id" || {_id isEqualTo ""}) then {
            _id = "unregistered";
        };

        // Save the original transform.
        private _position = (_entity get3DENAttribute "position") param [0, []];
        private _rotation = (_entity get3DENAttribute "rotation") param [0, []];

        EOEX_var_TestLock = [
            _entity,
            _username,
            _mode,
            _id,
            _position,
            _rotation
        ];

        EOEX_var_TestLockCreated = _created;

        // Register the restore function globally for event handler access.
        EOEX_fnc_testLockRestoreTransform = _restoreTransform;

        call _startLockMonitor;
        call _showIndicator;

        _entity
    };

    case "hide": {
        call _removeIndicator;
    };

    case "show": {
        call _showIndicator;
    };

    case "unlock": {
        call _removeIndicator;
        call _stopLockMonitor;

        if (!isNil "EOEX_var_TestLock") then {
            EOEX_var_TestLock params ["_lockedEntity"];

            if (
                _deleteOnUnlock &&
                {missionNamespace getVariable ["EOEX_var_TestLockCreated", false]}
            ) then {
                if (
                    !(_lockedEntity isEqualType objNull) ||
                    {!isNull _lockedEntity}
                ) then {
                    delete3DENEntities [_lockedEntity];
                };
            };
        };

        EOEX_var_TestLock = nil;
        EOEX_var_TestLockCreated = nil;
        EOEX_var_TestLockIcons = nil;
        EOEX_var_TestLockIconsHidden = nil;
        EOEX_fnc_testLockRestoreTransform = nil;
    };

    default {
        [
            format [
                "ERROR: Unknown testLock action '%1' (use lock, unlock, hide or show)",
                _action
            ]
        ] call BIS_fnc_error;
    };
};
