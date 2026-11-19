// EOEX_fnc_watchAttributesEdit

// Eden has no 3DEN event for the attributes window, so it is polled. The window is
// "Display3DENEditAttributes" (idd 315), which Eden opens on a double-click of an entity.
//
// While the window is open the selected entities are reported to the other clients with
// StartAttributesEdit, and StopAttributesEdit is sent as soon as it closes or the selection changes.
// Entities another client is editing are not reported and their window is closed.
// The messages are only sent on a change: a lost release is recovered by `LocksReleased` when the
// holder disconnects or registers again (see ClientNetworkMethods.RegisterUserName), not by a timer.

if (!isNil "EOEX_var_AttributesWatchActive") exitWith {};

EOEX_var_AttributesWatchActive = true;

private _sentIds = [];

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
            [format ["%1 is editing %2. Wait until %1 closed it.", _lock # 1, _lock # 2], 1, 5, false, 0] call BIS_fnc_3DENNotification;

            findDisplay 315 closeDisplay 0;
            _open = false;
            _ids = [];
        };
    };

    if (_ids isEqualTo _sentIds) then { continue };

    // A release is always attempted: if the connection is gone, the other clients drop the lock when
    // this client registers again.
    if (_sentIds isNotEqualTo []) then {
        ["StopAttributesEdit", [_sentIds], true] call EOEX_fnc_callExtensionAsync;
    };

    if (_ids isNotEqualTo [] && {missionNamespace getVariable ["EOEX_var_Connected", false]}) then {
        // The client id lets the receivers release exactly this client's locks, even when both
        // clients use the same profile name.
        private _clientId = missionNamespace getVariable ["EOEX_var_clientID", -1];

        if (!(_clientId isEqualType 0)) then { _clientId = -1 };

        ["StartAttributesEdit", [_ids, profileNameSteam, _clientId], true] call EOEX_fnc_callExtensionAsync;
    };

    _sentIds = _ids;
};
