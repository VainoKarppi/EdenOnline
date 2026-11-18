// EOEX_fnc_moveInterpolation
if !(isNil "Eden_RemoteMoveEachFrame") then {
    removeMissionEventHandler ["EachFrame", Eden_RemoteMoveEachFrame];
};

Eden_RemoteMoveEachFrame = addMissionEventHandler ["EachFrame", {
        if !(missionNamespace getVariable ["Eden_RemoteMoveActive", false]) exitWith {};

        private _targets = missionNamespace getVariable ["Eden_RemoteMoveTargets", []];

        if (_targets isEqualTo []) exitWith {};

        private _lerpSpeed = 12;
        private _factor = 1 - exp (-_lerpSpeed * diag_deltaTime);

        // Objects, markers (names), comments (indices) and waypoints ([group, index]) all support
        // the Eden Position attribute, but only objects may be passed to isNull.
        private _isEntityValid = {
            params ["_entity"];

            if (_entity isEqualType objNull) exitWith { !(isNull _entity) };
            if (_entity isEqualType "") exitWith { _entity in (all3DENEntities # 5) }; // markers
            if (_entity isEqualType 0) exitWith { _entity in (all3DENEntities # 7) }; // comments
            if (_entity isEqualType []) exitWith { (count _entity) == 2 }; // waypoints

            false
        };

        {
            private _object = _x select 0;
            private _targetPosition = _x select 1;

            if ([_object] call _isEntityValid) then {
                private _currentPosition = (_object get3DENAttribute "Position") param [0, nil];

                if (!isNil "_currentPosition") then {
                    private _difference = _targetPosition vectorDiff _currentPosition;

                    if ((vectorMagnitude _difference) > 0.001) then {
                        private _newPosition = _currentPosition vectorAdd (_difference vectorMultiply _factor);

                        _newPosition set [2, (_newPosition select 2) max 0];

                        _object set3DENAttribute ["Position", _newPosition];
                    };
                };
            };
        } forEach _targets;
    }
];
