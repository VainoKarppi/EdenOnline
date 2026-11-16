class CfgPatches
{
    class Scripts
    {
        units[] = {};
        weapons[] = {};
        requiredAddons[] = {"A3_Functions_F", "3DEN"};
        requiredVersion = 2.22;
        author[] = {"Razer"};
    };
};

class CfgFunctions
{
    class EdenOnline {
        tag = "EOEX";

        class Functions_Extension {
            file = "z\eoex\addons\functions\extension";

            class handleExtensionMessage {};
            class callExtension {};
            class callExtensionAsync {};
        };

        class Functions_Extension_Init {
            file = "z\eoex\addons\functions\extension\init";

            // Must not be recompiled.
            class initExtension {};
            class initExtensionEvents {};
            class init3DENEvents {};
        };

        class Functions_3DEN_Events {
            file = "z\eoex\addons\functions\3denEvents";

            class init {};
            class onTerrainNew {};
            class onBeforeMissionPreview {};
            class onMissionPreviewEnd {};
            class onMissionLoad {};
            class onMissionNew {};
            class onMissionPreview {};
            class onMissionSave {};
            class onMissionAutoSave {};
        };

        class Functions_Objects_Helpers {
            file = "z\eoex\addons\functions\objects\helpers";

            class getId {};
            class hasId {};

            class getObjectType {};
            class getTypeIndex {};

            class addSkipAttributeChange {};
            class checkSkipAttributeChange {};
        };

        class Functions_Objects_CRUD {
            file = "z\eoex\addons\functions\objects\crud";

            class sendCreateObject {};
            class sendDeleteObject {};
            class sendObjectAttributes {};

            class receiveObjectCreate {};
            class receiveObjectUpdate {};
            class receiveObjectDelete {};
        };

        class Functions_Objects_Move {
            file = "z\eoex\addons\functions\objects\move";

            class moveInterpolation {};

            class receiveDragStart {};
            class receiveDragUpdate {};
            class receiveDragEnd {};

            class onEntityDragStart {};
            class onEntityDragEnd {};
        };

        class Functions_Objects_Sync {
            file = "z\eoex\addons\functions\objects\sync";

            class createSyncConnection {};
            class removeSyncConnection {};

            class receiveCreateSyncConnection {};
            class receiveRemoveSyncConnection {};
        };

        class Functions_Objects_Markers {
            file = "z\eoex\addons\functions\objects\markers";

            class sendCreateMarker {};
            class sendDeleteMarker {};
            class sendUpdateMarker {};
        };

        class Functions_Mission {
            file = "z\eoex\addons\functions\mission";

            class sendMissionAttributes {};
            class receiveMissionAtrribute {};
        };

        class Functions_Server {
            file = "z\eoex\addons\functions\server";

            class startServer {};
            class connect {};
            class disconnect {};
        };

        class Functions_UI {
            file = "z\eoex\addons\functions\ui";

            class drawCameras {};

            class showConnectDialog {};
            class togglePlayButtons {};

            class showPlayersDialog {};
            class updateClientList {};

            class handleLoadingScreen {};
        };
    };
};

class Cfg3DEN
{
    class EventHandlers
    {
        class EXT
        {
            // TODO Set these
            // TODO Run disconnect when exiting from 3DEN
            init = "call EOEX_fnc_init";
            onBeforeMissionPreview = "call EOEX_fnc_onBeforeMissionPreview";
            onMissionAutoSave = "call EOEX_fnc_onMissionAutoSave";
            onMissionLoad = "call EOEX_fnc_onMissionLoad";
            onMissionNew = "call EOEX_fnc_onMissionNew";
            onMissionPreview = "call EOEX_fnc_onMissionPreview";
            OnMissionPreviewEnd = "call EOEX_fnc_onMissionPreviewEnd";
            onMissionSave = "call EOEX_fnc_onMissionSave";
            onTerrainNew = "call EOEX_fnc_onTerrainNew";
        };
    };
};

class ctrlMenuStrip;
class Display3DEN
{
    onUnload = "[1] call EOEX_fnc_disconnect;\
    [""onUnload"", _this, ""Display3DEN"", '3DENDisplays'] call (uinamespace getvariable 'BIS_fnc_initDisplay');";

    class Controls
    {
        class MenuStrip : ctrlMenuStrip
        {
            class Items
            {
                class Tools
                {
                    items[] += {"EOEX_EdenOnline"};
                };

                class EOEX_EdenOnline
                {
                    text = "Eden Online";
                    picture = "\a3\3DEN\Data\Displays\Display3DEN\StatusBar\server_ca.paa";
                    action = "call EOEX_fnc_showConnectDialog;";
                    opensNewWindow = 1;
                };
            };
        };
    };
};