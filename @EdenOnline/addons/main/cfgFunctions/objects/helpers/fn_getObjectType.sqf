// EOEX_fnc_getObjectType
params ["_object"];

// [objects, groups, triggers, systems, waypoints, markers, layers, comments]

if (isNil "_object") exitWith {};

if (_object isEqualType objNull && {_object isKindOf "EmptyDetector"}) exitWith {"Trigger"};
if (_object isEqualType objNull && {_object isKindOf "Logic"}) exitWith {"Logic"};

if (_object isEqualType objNull) exitWith {"Object"}; // Has to be object, if not Trigger or Logic

if (_object isEqualType grpNull) exitWith {"Group"};
if (_object isEqualType "") exitWith {"Marker"};
if (_object isEqualType []) exitWith {"Waypoint"};

// Layer or comment

private _allEntities = all3DENEntities;
if (_object in _allEntities # 5) exitWith {"Layer"};
if (_object in _allEntities # 6) exitWith {"Comment"};
