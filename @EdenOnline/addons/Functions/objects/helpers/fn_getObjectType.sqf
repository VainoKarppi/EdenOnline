// EOEX_fnc_getObjectType
params ["_entity", "_typeName"];

if (isNil "_typeName") then {
    private _typeName = typeName _entity;
};

// [objects, groups, triggers, systems, waypoints, markers, layers, comments]

if (isNil "_entity") exitWith {};

if (_entity isEqualType objNull && {_entity isKindOf "EmptyDetector"}) exitWith {"Trigger"};
if (_entity isEqualType objNull && {_entity isKindOf "Logic"}) exitWith {"Logic"};

if (_entity isEqualType objNull) exitWith {"Object"}; // Has to be object, if not Trigger or Logic

if (_entity isEqualType grpNull) exitWith {"Group"};
if (_entity isEqualType "") exitWith {"Marker"};
if (_entity isEqualType []) exitWith {"Waypoint"};

// Layer or comment
if (_entity isEqualTo -1) exitWith {"Layer"};

private _allEntities = all3DENEntities;
if (_entity in _allEntities # 6) exitWith {"Layer"};
if (_entity in _allEntities # 7) exitWith {"Comment"};