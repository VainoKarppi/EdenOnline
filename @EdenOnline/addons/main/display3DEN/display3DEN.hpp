class ctrlMenuStrip;
class Display3DEN {
    onUnload = "[1] call EOEX_fnc_disconnect;\
    [""onUnload"", _this, ""Display3DEN"", '3DENDisplays'] call (uinamespace getvariable 'BIS_fnc_initDisplay');";

    class Controls {
        class MenuStrip : ctrlMenuStrip {
            class Items {
                class Tools {
                    items[] += {"EOEX_EdenOnline"};
                };

                class EOEX_EdenOnline {
                    text = "Eden Online";
                    picture = "\a3\3DEN\Data\Displays\Display3DEN\StatusBar\server_ca.paa";
                    action = "call EOEX_fnc_showConnectDialog;";
                    opensNewWindow = 1;
                };
            };
        };
    };
};