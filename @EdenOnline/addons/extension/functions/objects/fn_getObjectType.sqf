// EOEX_fnc_getObjectType
params ["_object", ["_allEntities", all3DENEntities]];

if (isNull _object) exitWith {};

if (_object in _allEntities # 0) exitWith {"Object"};
if (_object in _allEntities # 1) exitWith {"Group"};
if (_object in _allEntities # 2) exitWith {"Trigger"};
if (_object in _allEntities # 3) exitWith {"Waypoint"};
if (_object in _allEntities # 4) exitWith {"Marker"};
if (_object in _allEntities # 5) exitWith {"Layer"};
if (_object in _allEntities # 6) exitWith {"Comment"};
