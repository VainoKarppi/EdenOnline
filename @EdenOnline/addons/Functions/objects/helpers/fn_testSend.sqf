// call EOEX_fnc_testSend

// Simulates local Eden editing for every supported entity type, so the outgoing path
// (3DEN event -> extension -> server) can be exercised. There is currently no other client
// listening, so nothing is echoed back: confirm the SENT DATA blocks in the log.

[] spawn {
    if !(missionNamespace getVariable ["EOEX_var_Connected", false]) exitWith {
        diag_log "[EdenOnline] testSend: not connected, start a server first";
    };

    private _center = [500, 500, 0];

    // * CREATE
    diag_log "[EdenOnline] testSend: creating entities";

    private _layer = -1 add3DENLayer "EOEX_testSendLayer";
    private _unit = create3DENEntity ["Object", "B_Soldier_GL_F", _center];
    // Crewed vehicles are reported to Eden twice (group + vehicle), which must not send the object twice.
    private _vehicle = create3DENEntity ["Object", "B_APC_Tracked_01_CRV_F", _center vectorAdd [20, 0, 0]];
    private _emptyVehicle = create3DENEntity ["Object", "B_APC_Tracked_01_rcws_F", _center vectorAdd [30, 0, 0], true];
    private _trigger = create3DENEntity ["Trigger", "EmptyDetector", _center vectorAdd [40, 0, 0]];
    private _logic = create3DENEntity ["Logic", "ModuleAnimals_F", _center vectorAdd [60, 0, 0]];
    private _marker = create3DENEntity ["Marker", "mil_destroy", _center vectorAdd [0, 20, 0]];
    private _comment = create3DENEntity ["Comment", "loc_talk", _center vectorAdd [20, 20, 0]];
    sleep 0.5;

    private _waypoint = (group _unit) create3DENEntity ["Waypoint", "Move", _center vectorAdd [10, 10, 0]];
    sleep 0.5;

    // Eden groups a unit placed within 5 m of another unit; replicate that here.
    private _groupMember = create3DENEntity ["Object", "B_Soldier_F", _center vectorAdd [3, 0, 0]];
    sleep 0.3;
    add3DENConnection ["Group", [_groupMember], group _unit];
    sleep 0.3;

    // Eden reports a new unit through its group, which EOEX_fnc_handleCreateObject ignores,
    // so send the creation explicitly for every test entity.
    {
        [_x] spawn EOEX_fnc_handleCreateObject;
    } forEach [_layer, _unit, _groupMember, _vehicle, _emptyVehicle, _trigger, _logic, _marker, _comment, _waypoint];
    sleep 1;

    // * UPDATE
    diag_log "[EdenOnline] testSend: updating attributes";

    _unit set3DENAttribute ["Name", "EOEX_testSendUnit"];
    _unit set3DENAttribute ["Position", _center vectorAdd [5, 0, 0]];
    _groupMember set3DENAttribute ["Position", _center vectorAdd [6, 0, 0]];
    _vehicle set3DENAttribute ["Ammo", 0.5];
    _emptyVehicle set3DENAttribute ["Ammo", 0.25];
    _trigger set3DENAttribute ["Size3", [100, 100, -1]];
    _trigger set3DENAttribute ["IsRectangle", true];
    _logic set3DENAttribute ["Name", "EOEX_testSendLogic"];
    _marker set3DENAttribute ["Text", "EOEX_testSendMarker"];
    _comment set3DENAttribute ["Name", "EOEX_testSendComment"];
    _waypoint set3DENAttribute ["Description", "EOEX_testSendWaypoint"];
    sleep 1;

    // * CLASS CHANGE
    // Eden deletes and recreates an entity when its class changes, so no attribute event is
    // raised for it. Send the update explicitly; receiveObjectUpdate recreates the entity.
    diag_log "[EdenOnline] testSend: sending class change for the vehicle";

    private _vehicleId = [_vehicle, true] call EOEX_fnc_getId;
    private _classChange = createHashMapFromArray [["ItemClass", "B_APC_Wheeled_01_cannon_F"]];
    ["UpdateObject", [_vehicleId, "Object", _classChange], true] call EOEX_fnc_callExtensionAsync;
    sleep 0.5;

    // * DELETE
    diag_log "[EdenOnline] testSend: deleting entities";

    delete3DENEntities [_waypoint];
    sleep 0.3;
    delete3DENEntities [_comment, _marker, _trigger, _logic, _vehicle, _emptyVehicle, _groupMember, _unit];
    sleep 0.3;
    delete3DENEntities [_layer];

    diag_log "[EdenOnline] testSend finished";
};
