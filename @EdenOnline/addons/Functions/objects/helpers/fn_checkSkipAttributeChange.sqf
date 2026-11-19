// EOEX_fnc_checkSkipAttributeChange


params ["_id", "_change"];

private _changes = EOEX_var_SkipAttributeChanges getOrDefault [_id, []];

private _valuesMatch = {
    params ["_left", "_right"];

    if (_left isEqualTo _right) exitWith { true };

    // Eden quantizes some values when it applies them (Position to 1 cm, Rotation to a slightly
    // different float), so the change event rarely carries exactly the value that was received.
    if ((_left isEqualType 0) && {_right isEqualType 0}) exitWith { abs (_left - _right) <= 0.02 };

    if (!(_left isEqualType []) || {!(_right isEqualType [])} || {count _left != count _right}) exitWith { false };

    private _match = true;

    {
        private _leftValue = _left # _forEachIndex;

        if (_leftValue isEqualType 0 && {_x isEqualType 0}) then {
            if (abs (_leftValue - _x) > 0.02) then { _match = false };
        } else {
            if (_leftValue isNotEqualTo _x) then { _match = false };
        };
    } forEach _right;

    _match
};

private _index = _changes find _change;

// Fall back to a tolerant comparison of the same property.
if (_index < 0) then {
    private _property = _change param [0, ""];
    private _value = _change param [1, nil];

    {
        if ((_x param [0, ""]) isEqualTo _property && {[_x param [1, nil], _value] call _valuesMatch}) exitWith {
            _index = _forEachIndex;
        };
    } forEach _changes;
};

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
