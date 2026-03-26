class CfgPatches
{
	class Scripts
	{
		units[] = {};
		weapons[] = {};
		requiredAddons[] = { "A3_Functions_F", "3DEN"};
		requiredVersion = 1.0;
		fileName = "extension.pbo";
		author[]= {"Razer"};
	};
};



class CfgFunctions
{
	class Extension {
		tag = "EXT";
		class Functions_Main
		{
			file = "\extension\functions";
			class init3DENEvents {};
		};
		class Functions_Camera
		{
			file = "\extension\functions\camera";
			class drawCameras {};
		};
		class Functions_Extension
		{
			file = "\extension\functions\extension";
			class initExtension {};
            class callExtension {};
			class callExtensionAsync {};
            class initExtensionEvents {};
		};
		class Functions_Objects
		{
			file = "\extension\functions\objects";
            class createObject {};
            class deleteObject {};
            class updateObjectAttributes {};

			class getId {};
		};
		class Functions_Server
		{
			file = "\extension\functions\server";
			class startServer {};
			class connect {};
			class disconnect {};
		};
		class Functions_UI
		{
			file = "\extension\functions\ui";
			class showConnectDialog {};
		};
	};
};





class Cfg3DEN
{
    class EventHandlers
    {
        class EXT
        {
            init = "call EXT_fnc_initExtension";
            onTerrainNew = "call ENH_fnc_EH_onTerrainNew";
            onMissionPreviewEnd = "call ENH_fnc_EH_onMissionPreviewEnd";
            onMissionLoad = "call ENH_fnc_EH_onMissionLoad";
            onMissionNew = "call ENH_fnc_EH_onMissionNew";
            onMissionPreview = "call ENH_fnc_EH_onMissionPreview";
            onMissionSave = "call ENH_fnc_createBackupMissionSQM";
            onMissionAutoSave = "call ENH_fnc_createBackupMissionSQM";
        };
    };
};

class ctrlMenuStrip;
class display3DEN
{
	onUnload="[1] call EXT_fnc_disconnect;[""onUnload"",_this,""Display3DEN"",'3DENDisplays'] call (uinamespace getvariable 'BIS_fnc_initDisplay');";

	class Controls
	{
		class MenuStrip : ctrlMenuStrip
		{
			class Items
			{
				class Tools
				{
					items[] += {"EXT_EdenOnline"};
				};

				class EXT_EdenOnline
				{
					text = "Eden Online";
					picture = "\a3\3DEN\Data\Controls\ctrlMenu\link_ca.paa"; // TODO
					action = "[] spawn EXT_fnc_showConnectDialog;";
				};
			};
		};
	};
};

class RscText;
class RscEdit;
class RscButton;
class CfgDialogs {

    class EXT_ConnectDialog {
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
                action = "[] call EXT_fnc_onConnect;";
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