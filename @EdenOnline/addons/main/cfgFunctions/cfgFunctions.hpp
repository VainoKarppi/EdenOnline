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

            class addSkipAttributeChange {};
            class checkSkipAttributeChange {};
            class getId {};
            class getObjectType {};
            class getTypeIndex {};
            class hasId {};
        };

        class Functions_Objects_CRUD {
            file = "z\eoex\addons\functions\objects\crud";

            class receiveObjectCreate {};
            class receiveObjectDelete {};
            class receiveObjectUpdate {};
            class sendCreateObject {};
            class sendDeleteObject {};
            class sendObjectAttributes {};
        };

        class Functions_Objects_Move {
            file = "z\eoex\addons\functions\objects\move";

            class moveInterpolation {};
            class onEntityDragEnd {};
            class onEntityDragStart {};
            class receiveDragEnd {};
            class receiveDragStart {};
            class receiveDragUpdate {};
        };

        class Functions_Objects_Sync {
            file = "z\eoex\addons\functions\objects\sync";

            class createSyncConnection {};
            class receiveCreateSyncConnection {};
            class receiveRemoveSyncConnection {};
            class removeSyncConnection {};
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

            class connect {};
            class disconnect {};
            class startServer {};
        };

        class Functions_UI {
            file = "z\eoex\addons\functions\ui";

            class drawCameras {};
            class handleLoadingScreen {};
            class showConnectDialog {};
            class showPlayersDialog {};
            class togglePlayButtons {};
            class updateClientList {};
        };
    };
};