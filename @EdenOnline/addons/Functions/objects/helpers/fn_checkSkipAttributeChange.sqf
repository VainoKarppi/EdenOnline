

params ["_id", "_change"];

private _changes = EOEX_var_SkipAttributeChanges getOrDefault [_id, []];
private _index = _changes find _change;

if (_index < 0) exitWith { false };

// Remove the matched change
_changes deleteAt _index;

// Remove ID entirely if no changes remain
if (_changes isEqualTo []) then {
    EOEX_var_SkipAttributeChanges deleteAt _id;
} else {
    EOEX_var_SkipAttributeChanges set [_id, _changes];
};

true