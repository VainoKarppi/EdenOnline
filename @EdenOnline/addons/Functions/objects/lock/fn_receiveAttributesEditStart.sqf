// EOEX_fnc_receiveAttributesEditStart

// A client opened the attributes window of the entities: lock them here so they cannot be edited or
// dragged locally until the same client sends StopAttributesEdit.

params [["_ids", [], [[]]], ["_username", "another client", [""]]];

if (_ids isEqualTo []) exitWith {};

{
    [_x, _username, "attributes"] call EOEX_fnc_lockEntity;
} forEach _ids;

[format ["%1 opened the attributes of %2. Locked until %1 closes it.", _username, _ids joinString ", "], 1, 6] call BIS_fnc_3DENNotification;
