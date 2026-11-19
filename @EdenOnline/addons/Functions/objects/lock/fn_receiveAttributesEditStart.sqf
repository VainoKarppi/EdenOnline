// EOEX_fnc_receiveAttributesEditStart

// A client opened the attributes window of the entities: lock them here so they cannot be edited or
// dragged locally until the same client releases them. The message is repeated as a heartbeat, so
// only the first one for an entity is announced.

params [["_ids", [], [[]]], ["_username", "another client", [""]], ["_clientId", -1, [0]]];

if (_ids isEqualTo []) exitWith {};

private _locks = missionNamespace getVariable ["EOEX_var_Locks", createHashMap];

private _new = _ids select { (_locks getOrDefault [_x, []]) isEqualTo [] };

{
    [_x, _username, "attributes", _clientId] call EOEX_fnc_lockEntity;
} forEach _ids;

if (_new isEqualTo []) exitWith {};

[format ["%1 opened the attributes of %2. Locked until %1 closes it.", _username, _new joinString ", "], 1, 6, false, 0] call BIS_fnc_3DENNotification;
