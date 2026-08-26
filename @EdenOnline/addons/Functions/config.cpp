class CfgPatches
{
	class Scripts
	{
		units[] = {};
		weapons[] = {};
		requiredAddons[] = { "A3_Functions_F", "3DEN"};
		requiredVersion = 2.22;
		author[]= {"Razer"};
	};
};



class CfgFunctions
{
    class EdenOnline {
        tag = "EOEX";
        class Functions_Camera {
            file = "Functions\camera";
            class drawCameras { recompile = 1; };
        };
        class Functions_Extension {
            file = "Functions\extension";

            // Must not be recompiled.
            class initExtension {};
            class reloadFunctions {};
            class reloadFunctionFromFile {};
            

            class init3DENEvents { recompile = 1; };
            class callExtension { recompile = 1; };
            class callExtensionAsync { recompile = 1; };
            class initExtensionEvents { recompile = 1; };
            class handleExtensionMessage { recompile = 1; };
        };

        class Functions_Objects {
            file = "Functions\objects";

            class createObject { recompile = 1; };
            class deleteObject { recompile = 1; };
            class updateObjectAttributes { recompile = 1; };

            class getObjectType { recompile = 1; };

            class createSyncConnection { recompile = 1; };
            class removeSyncConnection { recompile = 1; };

            class onReceiveCreateSyncConnection { recompile = 1; };
            class onReceiveRemoveSyncConnection { recompile = 1; };

            class getId { recompile = 1; };

            class moveInterpolation { recompile = 1; };

            class receiveDragStart { recompile = 1; };
            class receiveDragUpdate { recompile = 1; };
            class receiveDragEnd { recompile = 1; };

            class onEntityDragStart { recompile = 1; };
            class onEntityDragEnd { recompile = 1; };
        };

        class Functions_Markers {
            file = "Functions\markers";

            class createMarker { recompile = 1; };
            class deleteMarker { recompile = 1; };
            class updateMarker { recompile = 1; };
        };

        class Functions_Triggers {
            file = "Functions\triggers";

            class createTrigger { recompile = 1; };
            class deleteTrigger { recompile = 1; };
            class updateTrigger { recompile = 1; };
        };

        class Functions_Mission {
            file = "Functions\mission";

            class updateMissionAttributes { recompile = 1; };
        };

        class Functions_Server {
            file = "Functions\server";

            class startServer { recompile = 1; };
            class connect { recompile = 1; };
            class disconnect { recompile = 1; };
        };

        class Functions_UI {
            file = "Functions\ui";

            class showConnectDialog { recompile = 1; };
            class togglePlayButtons { recompile = 1; };

            class showPlayersDialog { recompile = 1; };
            class updateClientList { recompile = 1; };
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
            init = "call EOEX_fnc_initExtension";
            onTerrainNew = "[1] call EOEX_fnc_disconnect; call EOEX_fnc_initExtension";
            OnMissionPreviewEnd = "call EOEX_fnc_initExtension";
            onMissionLoad = "diag_log str([3])";
            onMissionNew = "call EOEX_fnc_initExtension";
            onMissionPreview = "diag_log str([5])";
            onMissionSave = "diag_log str([6])";
            onMissionAutoSave = "diag_log str([7])";
        };
    };
};

class ctrlMenuStrip;
class display3DEN
{
	onUnload="[1] call EOEX_fnc_disconnect;[""onUnload"",_this,""Display3DEN"",'3DENDisplays'] call (uinamespace getvariable 'BIS_fnc_initDisplay');";

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
					picture = "\a3\3DEN\Data\Controls\ctrlMenu\link_ca.paa"; // TODO
					action = "[] spawn EOEX_fnc_showConnectDialog;";
				};
			};
		};
	};
};

class RscText;
class RscEdit;
class RscButton;
class CfgDialogs {

    class EOEX_ConnectDialog {
        idd = 5000;
        movingEnable = 0;
        enableSimulation = 1;

        class controlsBackground {
            class Background: RscText {
                x = 0.35; y = 0.35;
                w = 0.3;  h = 0.25;
                colorBackground[] = {0,0,0,0.7};
            };

            class Title: RscText {
                text = "Start Server";
                x = 0.35; y = 0.32;
                w = 0.3; h = 0.03;
                colorBackground[] = {0,0,0,0};
            };

            class PortLabel: RscText {
                text = "Port:";
                x = 0.37; y = 0.40;
                w = 0.1; h = 0.03;
                colorBackground[] = {0,0,0,0};
            };

            class PasswordLabel: RscText {
                text = "Password:";
                x = 0.37; y = 0.45;
                w = 0.1; h = 0.03;
                colorBackground[] = {0,0,0,0};
            };
        };

        class controls {
            class PortEdit: RscEdit {
                idc = 5001;
                x = 0.47; y = 0.40;
                w = 0.15; h = 0.03;
                text = "2302";
            };

            class PasswordEdit: RscEdit {
                idc = 5002;
                x = 0.47; y = 0.45;
                w = 0.15; h = 0.03;
            };

            class ConnectButton: RscButton {
                text = "Connect";
                x = 0.37; y = 0.52;
                w = 0.12; h = 0.04;
                action = "[] call EOEX_fnc_onConnect;";
            };

            class CancelButton: RscButton {
                text = "Cancel";
                x = 0.50; y = 0.52;
                w = 0.12; h = 0.04;
                action = "closeDialog 0;";
            };
        };
    };
};
