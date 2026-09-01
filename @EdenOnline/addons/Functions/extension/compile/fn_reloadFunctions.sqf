// EOEX_fnc_reloadFunctions


params [["_functionToCompile",""]];

private _cfgFunctions = configFile >> "CfgFunctions";
private _reloaded = 0;

{
    private _tagConfig = _x;
    private _tag = getText (_tagConfig >> "tag");

    if (_tag == "BIS") then {
        continue;
    };

    {
        private _categoryConfig = _x;
        private _file = getText (_categoryConfig >> "file");

        if (_file isEqualTo "") then {
            continue;
        };

        {
            private _functionConfig = _x;

            if ((getNumber (_functionConfig >> "recompile")) != 1) then {
                continue;
            };

            private _functionName = configName _functionConfig;

            private _variableName = format ["%1_fnc_%2",_tag, _functionName];


            // If a specific function was requested,
            // skip every function that does not match it.
            if (
                _functionToCompile != ""
                && { _functionToCompile != _variableName }
            ) then {
                continue;
            };

            private _filePath = format ["\z\%1\addons\%2\fn_%3.sqf",_tag, _file, _functionName];

            missionNamespace setVariable [_variableName, compile preprocessFileLineNumbers _filePath];
            
            if (_functionToCompile != "") then {
                diag_log format ["Reloaded function: %1", _variableName];
            };

            _reloaded = _reloaded + 1;

        } forEach ("true" configClasses _categoryConfig);

    } forEach ("true" configClasses _tagConfig);

} forEach ("true" configClasses _cfgFunctions);

if (_functionToCompile == "" && _reloaded > 0) then {
    diag_log format ["EOEX RELOAD: Reloaded %1 functions.", _reloaded];
};

(_reloaded > 0);
