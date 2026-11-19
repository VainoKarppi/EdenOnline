// EOEX_fnc_watchAttributesEdit

// Eden has no 3DEN event for the attributes window, so it is polled. The window is
// "Display3DENEditAttributes" (idd 315), which Eden opens on a double-click of an entity.
//
// While the window is open the selected entities are reported to the other clients with
// StartAttributesEdit, and StopAttributesEdit is sent as soon as it closes or the selection changes.
// The Start is repeated as a heartbeat: the receiving side drops an "attributes" lock that is not
// renewed in time, so a Stop that never arrives (crashed or restarted client) cannot leave an entity
// locked forever. Entities another client is editing are not reported and their window is closed.

if (!isNil "EOEX_var_AttributesWatchActive") exitWith {};

EOEX_var_AttributesWatchActive = true;

private _heartbeat = missionNamespace getVariable ["EOEX_var_LockHeartbeat", 1];

private _sentIds = [];
private _lastSend = 0;

while { true } do {
    sleep 0.25;

    private _open = !(isNull findDisplay 315);
    private _ids = [];

    if (_open) then {
        private _entities = [];

        // Query each type separately: get3DENSelected "" nests the categories and flattening would
        // break waypoint references into a group and an index.
        {
            _entities append (get3DENSelected _x);
        } forEach ["object", "trigger", "logic", "waypoint", "marker", "comment"];

        _entities = _entities select { !(_x isEqualType grpNull) };

        {
            private _id = [_x, true] call EOEX_fnc_getId;

            if (!isNil "_id" && {_id isNotEqualTo ""}) then { _ids pushBack _id };
        } forEach _entities;

        // Another client holds the entity: tell who and refuse to edit it.
        private _lock = nil;

        {
            private _entry = (missionNamespace getVariable ["EOEX_var_Locks", createHashMap]) getOrDefault [_x, []];

            if (!(_entry isEqualTo [])) exitWith { _lock = _entry };
        } forEach _ids;

        if (!isNil "_lock") then {
            [format ["%1 is editing %2. Wait until %1 closed it.", _lock # 1, _lock # 2], 1, 5] call BIS_fnc_3DENNotification;

            findDisplay 315 closeDisplay 0;
            _open = false;
            _ids = [];
        };
    };

    private _changed = _ids isNotEqualTo _sentIds;
    private _renew = _ids isNotEqualTo [] && {(diag_tickTime - _lastSend) > _heartbeat};

    if (!_changed && !_renew) then { continue };

    // A release is always attempted: if the connection is gone, the other clients drop the lock by
    // expiry instead.
    if (_changed && {_sentIds isNotEqualTo []}) then {
        ["StopAttributesEdit", [_sentIds], true] call EOEX_fnc_callExtensionAsync;
    };

    if (_ids isNotEqualTo [] && {missionNamespace getVariable ["EOEX_var_Connected", false]}) then {
        ["StartAttributesEdit", [_ids, profileNameSteam], true] call EOEX_fnc_callExtensionAsync;
        _lastSend = diag_tickTime;
    };

    _sentIds = _ids;
};
