// call EOEX_fnc_testReceive

[] spawn {

(findDisplay 316000) closeDisplay 1;

sleep 0.5;

[1] call EOEX_fnc_disconnect;

sleep 1;

call EOEX_fnc_initExtension;

[2302, ""] call EOEX_fnc_startServer;

waitUntil { sleep 0.1; missionNamespace getVariable ["EOEX_var_Connected", false] == true };

sleep 1;

// RECEIVE OBJECTS
["QI56X8K4","Object",[["ItemClass","B_Soldier_GL_F"],["Position",[4636.22,1131.84,0]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["CXCZIO8E","Trigger",[["ItemClass","EmptyDetector"],["Position",[4651.5,1928.26,0]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["HK6WCLSR","Logic",[["ItemClass","ModuleAnimals_F"],["Position",[5581.05,1296.44,0]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["KFCJ1X8C","Marker",[["ItemClass","mil_destroy"],["Position",[4903.78,1605.54,5]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["GJRZY90Y","Comment",[["ItemClass","loc_talk"],["Position",[4328.92,1233.38,5]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["M7TGTYM2","Waypoint",[["EOEX_Owner","QI56X8K4"],["ItemClass","Move"],["Position",[4713.9,1175.86,0]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["LSZP9FEE","Layer",[["Name","Layer 2"],["Transformation",true],["Visibility",true],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;
["QI56X8K6","Object",[["ItemClass","O_Soldier_GL_F"],["Position",[4536.22,1131.84,0]],["Layer","LSZP9FEE"]]] spawn EOEX_fnc_receiveObjectCreate;
["XH3CC3T9","Object",[["ItemClass","B_APC_Tracked_01_rcws_F"],["Position",[5609.42,1751.76,0]],["Layer",""],["EOEX_IS_EMPTY",true]]] spawn EOEX_fnc_receiveObjectCreate;
["OZX6ABI7","Marker",[["ItemClass","mil_destroy"],["Position",[4903.78,1605.54,5]],["Layer",""]]] spawn EOEX_fnc_receiveObjectCreate;

sleep 2;

// RECEIVE GROUPED UNITS
// Eden groups a unit placed within 5 m of another unit, so the create message names the leader.
["GRPA","Object",[["ItemClass","B_Soldier_F"],["Position",[4700,6500,0]],["Layer",""]]] call EOEX_fnc_receiveObjectCreate;
["GRPB","Object",[["ItemClass","B_Soldier_F"],["Position",[4703,6500,0]],["Layer",""],["EOEX_GROUP","GRPA"]]] call EOEX_fnc_receiveObjectCreate;

sleep 1;

private _groupUnitA = EOEX_var_Objects getOrDefault ["GRPA", objNull];
private _groupUnitB = EOEX_var_Objects getOrDefault ["GRPB", objNull];

diag_log format ["[EdenOnline] testReceive: grouped units share a group = %1", (group _groupUnitA) isEqualTo (group _groupUnitB)];

sleep 1;

// RECEIVE UPDATES
["QI56X8K6","Object",[["Layer",""]]] call EOEX_fnc_receiveObjectUpdate;
["QI56X8K6","Object",[["pitch",0.98],["speaker","Male02PER"],["Name","asdasd"]]] call EOEX_fnc_receiveObjectUpdate;
["3FQ4S1G2","Group",[["Position",[4697.21,1154.58,0]]]] call EOEX_fnc_receiveObjectUpdate;
["QI56X8K6","Object",[["Position",[4471.52,1284.96,0]]]] call EOEX_fnc_receiveObjectUpdate;
["CXCZIO8E","Trigger",[["Size3",[100,100,-1]]]] call EOEX_fnc_receiveObjectUpdate;
["HK6WCLSR","Logic",[["ModuleAnimals_F_Type","Hen_random_F"]]] call EOEX_fnc_receiveObjectUpdate;
["HK6WCLSR","Logic",[["Init","aadddd"],["Name","testtest"]]] call EOEX_fnc_receiveObjectUpdate;
["OZX6ABI7","Marker",[["Text","mymarker"],["BaseColor","Default"]]] call EOEX_fnc_receiveObjectUpdate;
["QI56X8K4","Object",[["Position",[4630.61,1510.41,0]]]] call EOEX_fnc_receiveObjectUpdate;

sleep 1;

// CLASS CHANGES (the entity is recreated, Eden cannot change ItemClass in place)

["KFCJ1X8C","Marker",[["ItemClass","mil_dot"],["BaseColor","Default"]]] call EOEX_fnc_receiveObjectUpdate;
["OZX6ABI7","Marker",[["ItemClass","mil_dot"],["BaseColor","Default"]]] call EOEX_fnc_receiveObjectUpdate;
["XH3CC3T9","Object",[["ItemClass","B_Heli_Transport_01_F"]]] call EOEX_fnc_receiveObjectUpdate;
["QI56X8K6","Object",[["ItemClass","B_Soldier_AR_F"]]] call EOEX_fnc_receiveObjectUpdate;

sleep 2;

// RECEIVE DELETIONS
["QI56X8K6","Object"] call EOEX_fnc_receiveObjectDelete;
["HK6WCLSR","Logic"] call EOEX_fnc_receiveObjectDelete;
["CXCZIO8E","Trigger"] call EOEX_fnc_receiveObjectDelete;
["KFCJ1X8C","Marker"] call EOEX_fnc_receiveObjectDelete;
["M7TGTYM2","Waypoint"] call EOEX_fnc_receiveObjectDelete;
["QI56X8K4","Object"] call EOEX_fnc_receiveObjectDelete;
["GJRZY90Y","Comment"] call EOEX_fnc_receiveObjectDelete;
["LSZP9FEE","Layer"] call EOEX_fnc_receiveObjectDelete;
["XH3CC3T9","Object"] call EOEX_fnc_receiveObjectDelete;
["OZX6ABI7","Marker"] call EOEX_fnc_receiveObjectDelete;
["GRPA","Object"] call EOEX_fnc_receiveObjectDelete;
["GRPB","Object"] call EOEX_fnc_receiveObjectDelete;
};