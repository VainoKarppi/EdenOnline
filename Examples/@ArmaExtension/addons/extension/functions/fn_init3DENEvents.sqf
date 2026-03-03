

diag_log "3DEN INITIALIZED";


// * OBJECTS
add3DENEventHandler ["OnEditableEntityAdded", {
	params ["_entity"];
	[_entity] spawn EXT_fnc_createObject;
}];

add3DENEventHandler ["OnEditableEntityRemoved", {
	params ["_entity"];
	[_entity] spawn EXT_fnc_deleteObject;
}];

add3DENEventHandler ["OnEntityDragged", {
	params ["_entity"];
	[_entity] spawn EXT_fnc_updateObjectPosition;
}];

add3DENEventHandler ["OnEntityAttributeChanged", {
	params ["_entity", "_property"];
}];

// * CONNECTIONS
add3DENEventHandler ["OnConnectingEnd", {
	params ["_class", "_from", "_to"];
}];

// * MISSION SETTINGS
add3DENEventHandler ["OnMissionAttributeChanged", {
	params ["_section", "_property"];
}];