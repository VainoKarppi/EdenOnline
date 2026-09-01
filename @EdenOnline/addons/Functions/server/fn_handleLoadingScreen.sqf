// EOEX_fnc_handleLoadingScreen
params ["_enable", "_progress", ["_timeout", 10]];

if (_enable) then {

    private _loadingScreen = uiNamespace getVariable ["EOEX_var_loadingScreen", false];

    if (!_loadingScreen) then {
        startLoadingScreen ["New client connecting..."];
        uiNamespace setVariable ["EOEX_var_loadingScreen", true];
    };

    progressLoadingScreen _progress;

    private _timeoutAt = diag_tickTime + _timeout;
    uiNamespace setVariable ["EOEX_var_loadingScreenTimeout", _timeoutAt];

    [_timeoutAt] spawn {
        params ["_timeoutAt"];

        uiSleep (_timeoutAt - diag_tickTime max 0);

        private _loadingScreen = uiNamespace getVariable ["EOEX_var_loadingScreen", false];
        private _currentTimeout = uiNamespace getVariable ["EOEX_var_loadingScreenTimeout", 0];

        if (_loadingScreen && {diag_tickTime >= _currentTimeout}) then {

            diag_log format ["EOEX: Loading screen force timeout after %1 seconds.", diag_tickTime - (_currentTimeout - _timeout)];

            endLoadingScreen;

            uiNamespace setVariable ["EOEX_var_loadingScreen", nil];
            uiNamespace setVariable ["EOEX_var_loadingScreenTimeout", nil];
        };
    };

} else {
    endLoadingScreen;

    uiNamespace setVariable ["EOEX_var_loadingScreen", nil];
    uiNamespace setVariable ["EOEX_var_loadingScreenTimeout", nil];
};
