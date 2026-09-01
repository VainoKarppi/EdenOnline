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

        {
            private _object = _x select 0;
            private _targetPosition = _x select 1;

            if !(isNull _object) then {
                private _currentPosition = (_object get3DENAttribute "Position") select 0;

                private _difference = _targetPosition vectorDiff _currentPosition;

                if ((vectorMagnitude _difference) > 0.001) then {
                    private _newPosition = _currentPosition vectorAdd (_difference vectorMultiply _factor);

                    _newPosition set [2, (_newPosition select 2) max 0];

                    _object set3DENAttribute ["Position", _newPosition];
                };
            };
        } forEach _targets;
    }
];
