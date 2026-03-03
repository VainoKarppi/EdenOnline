class CfgPatches
{
	class Scripts
	{
		units[] = {};
		weapons[] = {};
		requiredAddons[] = {"A3_Functions_F"};
		fileName = "extension.pbo";
		requiredVersion = 1;
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
			preinit = 1;
			class init {};
		};
		class Functions_Extension
		{
			file = "\extension\functions\extension";
            class callExtension {};
			class callExtensionAsync {};
            class initEvents {};
			class createAsyncId {};
		};
	};

	class Editor {
		tag = "EXT";
		class Functions_Main
		{
			file = "\scripts\functions";
			preinit = 1;
			class init3DEN {}; // Called from Display3DEN:control
		};
		class Functions_Extension
		{
			file = "\scripts\functions\extension";
            class callExtension {};
			class callExtensionAsync {};
            class initEvents {};
			class createId {};
		};
        class Functions_Objects
		{
			file = "\scripts\functions\objects";
            class createObject {};
            class deleteObject {};
            class updateObject {};
			//class getId {};
		};
		class Functions_UI{
			file = "\scripts\functions\ui";

		};
		class Functions_Server{
			file = "\scripts\functions\server";
			class disconnect {};
		};
	};
};
