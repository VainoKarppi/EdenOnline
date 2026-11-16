// EOEX_fnc_getTypeIndex

params ["_type"];

// [objects, groups, triggers, systems, waypoints, markers, layers, comments]

/*
    create3DENEntity [mode, class, position, isEmpty]
    mode: String - can be "Object", "Trigger", "Waypoint", "Logic", "Marker" or "Comment"
*/

private _types = [
    "object",
    "group",
    "trigger",
    "logic",
    "waypoint",
    "marker",
    "layer",
    "comment"
];

(_types find (toLower _type))
