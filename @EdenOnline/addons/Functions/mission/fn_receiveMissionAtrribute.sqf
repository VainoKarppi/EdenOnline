// EOEX_fnc_receiveMissionAtrribute


params ["_section", "_property", "_value"];

// Mark as skipped to avoid infinite loop from mission attribute change event
EOEX_var_SkipAttributeChange set [[_section, _property], _value];

_section set3DENMissionAttribute [_property, _value];
