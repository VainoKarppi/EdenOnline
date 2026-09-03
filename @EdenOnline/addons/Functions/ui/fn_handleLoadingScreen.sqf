

params ["_enable", "_progress"];


if (_enable) then {
    if (isNil "EOEX_var_loadingScreen") then {
        startLoadingScreen ["New client connecting..."];
    };
    EOEX_var_loadingScreen = true;
    progressLoadingScreen _progress;
} else {
    endLoadingScreen;
    EOEX_var_loadingScreen = nil;
};
