// EOEX_fnc_unlockEntity

params [["_id", "", [""]]];

if (_id isEqualTo "" || {isNil "EOEX_var_Locks"}) exitWith {};

EOEX_var_Locks deleteAt _id;

if ((count EOEX_var_Locks) == 0 && {!isNil "EOEX_var_LocksHandler"}) then {
    removeMissionEventHandler ["Draw3D", EOEX_var_LocksHandler];
    EOEX_var_LocksHandler = nil;
};

if ((count EOEX_var_Locks) == 0 && {!isNil "EOEX_var_LocksEnforceHandler"}) then {
    removeMissionEventHandler ["EachFrame", EOEX_var_LocksEnforceHandler];
    EOEX_var_LocksEnforceHandler = nil;
};
