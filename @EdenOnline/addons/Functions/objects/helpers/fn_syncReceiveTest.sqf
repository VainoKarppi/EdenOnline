// call EOEX_fnc_syncReceiveTest

// Simulates the incoming CreateSyncConnection / RemoveSyncConnection messages for two objects
// that were created the same way a remote client creates them.

[] spawn {
    private _center = [520, 520, 0];

    ["SYNCA", "Object", [["ItemClass", "B_Soldier_F"], ["Position", _center], ["EOEX_Layer", ""]]] call EOEX_fnc_receiveObjectCreate;
    ["SYNCB", "Object", [["ItemClass", "B_Soldier_F"], ["Position", _center vectorAdd [10, 0, 0]], ["EOEX_Layer", ""]]] call EOEX_fnc_receiveObjectCreate;
    sleep 0.2;

    private _from = EOEX_var_Objects getOrDefault ["SYNCA", objNull];
    private _to = EOEX_var_Objects getOrDefault ["SYNCB", objNull];

    if (isNull _from || {isNull _to}) exitWith {
        diag_log "[EdenOnline] syncReceiveTest: test objects were not created";
    };

    private _hasConnection = {
        params ["_source", "_target"];
        (get3DENConnections _source) findIf {
            (_x select 0) == "Sync" && {(_x select 1) isEqualTo _target}
        } > -1
    };

    diag_log "[EdenOnline] syncReceiveTest: receiving connection";

    ["SYNCA", "SYNCB", "Sync"] call EOEX_fnc_receiveCreateSyncConnection;
    sleep 0.2;

    diag_log format ["[EdenOnline] syncReceiveTest: connection exists = %1", [_from, _to] call _hasConnection];

    diag_log "[EdenOnline] syncReceiveTest: receiving connection removal";

    ["SYNCA", "SYNCB", "Sync"] call EOEX_fnc_receiveRemoveSyncConnection;
    sleep 0.2;

    diag_log format ["[EdenOnline] syncReceiveTest: connection still exists = %1", [_from, _to] call _hasConnection];

    ["SYNCA", "Object"] call EOEX_fnc_receiveObjectDelete;
    ["SYNCB", "Object"] call EOEX_fnc_receiveObjectDelete;

    diag_log "[EdenOnline] syncReceiveTest finished";
};
