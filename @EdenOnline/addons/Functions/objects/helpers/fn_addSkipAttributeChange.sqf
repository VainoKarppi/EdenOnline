// EOEX_fnc_addSkipAttributeChange
params ["_id", "_change", ["_removeAfter", 1]];

private _changes = EOEX_var_SkipAttributeChanges getOrDefault [_id, []];

if !(_change in _changes) then {
    _changes pushBack _change;
    EOEX_var_SkipAttributeChanges set [_id, _changes];

    if (_removeAfter <= -1) exitWith {}; // Do not remove automatically

    // Force-remove this exact change after 1 second
    [_id, _change, _removeAfter] spawn {
        params ["_id", "_change", "_removeAfter"];

        sleep _removeAfter;

        private _changes = EOEX_var_SkipAttributeChanges getOrDefault [_id, []];

        private _index = _changes find _change;

        if (_index >= 0) then {
            _changes deleteAt _index;

            if (_changes isEqualTo []) then {
                EOEX_var_SkipAttributeChanges deleteAt _id;
            } else {
                EOEX_var_SkipAttributeChanges set [_id, _changes];
            };
        };
    };
};
