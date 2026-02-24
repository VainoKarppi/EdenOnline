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
};