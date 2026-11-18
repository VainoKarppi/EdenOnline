// call EOEX_fnc_syncSendTest

// Creates two objects, connects them with an Eden sync connection and removes the connection
// again, so the outgoing CreateSyncConnection / RemoveSyncConnection messages can be verified.
// There is currently no other client listening, so check the SENT DATA blocks in the log.

[] spawn {
    if !(missionNamespace getVariable ["EOEX_var_Connected", false]) exitWith {
        diag_log "[EdenOnline] syncSendTest: not connected, start a server first";
    };

    private _center = [500, 500, 0];

    private _from = create3DENEntity ["Object", "B_Soldier_F", _center];
    private _to = create3DENEntity ["Object", "B_Soldier_F", _center vectorAdd [10, 0, 0]];
    sleep 0.5;

    // Units are reported to Eden only through their group, so register and send them explicitly.
    {
        [_x] spawn EOEX_fnc_handleCreateObject;
    } forEach [_from, _to];
    sleep 0.5;

    diag_log "[EdenOnline] syncSendTest: creating connection";

    // Same shape Eden passes to OnConnectingEnd: [class, [sources], target]
    ["Sync", [[_from]], _to] call EOEX_fnc_createSyncConnection;
    sleep 0.5;

    diag_log "[EdenOnline] syncSendTest: removing connection";

    ["Sync", [[_from]], _to] call EOEX_fnc_removeSyncConnection;
    sleep 0.5;

    delete3DENEntities [_from, _to];

    diag_log "[EdenOnline] syncSendTest finished";
};
