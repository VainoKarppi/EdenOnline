
params ["_section","_property","_value"];

systemChat str (_this);

["SetMissionAttribute", _this, true] call EXT_fnc_callExtensionAsync;