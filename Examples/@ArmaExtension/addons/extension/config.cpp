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
			class init {};
			class init3DENEvents {};
		};
		class Functions_Camera
		{
			file = "\extension\functions\camera";
			class drawCamera {};
		};
		class Functions_Extension
		{
			file = "\extension\functions\extension";
			class initExtension {};
            class callExtension {};
			class callExtensionAsync {};
            class initExtensionEvents {};
			class createAsyncId {};
		};
		class Functions_Objects
		{
			file = "\extension\functions\objects";
            class createObject {};
            class deleteObject {};
			
            class updateObjectAttributes {};
			class updateObjectPosition {};

			class getId {};
		};
		class Functions_Server
		{
			file = "\extension\functions\server";
			class startServer {};
			class connect {};
			class disconnect {};
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
	onUnload="[1] call EOE_fnc_disconnect;[""onUnload"",_this,""Display3DEN"",'3DENDisplays'] call 	(uinamespace getvariable 'BIS_fnc_initDisplay')"

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
					text = "Eden Online Tools...";
					items[] = { "EXT_StartServer", "EXT_Connect" }; // Links to items inside the folder
				};

				class EXT_StartServer
				{
					text = "Start Server";
					picture = "\ArmaExtension\logo.paa";
					action = "[] spawn EXT_fnc_startServer;";
				};

				class EXT_Connect
				{
					text = "Connect Server";
					picture = "\ArmaExtension\logo.paa";
					action = "[] spawn EXT_fnc_connect;";
				};
			};
		};
	};
};