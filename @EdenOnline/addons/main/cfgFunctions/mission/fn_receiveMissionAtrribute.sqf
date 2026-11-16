// EOEX_fnc_receiveMissionAtrribute


params ["_section", "_property", "_value"];

// Mark this change to be skipped when the mission attribute is updated
["MISSION", [_section, _property, _value]] call EOEX_fnc_addSkipAttributeChange;

_section set3DENMissionAttribute [_property, _value];
