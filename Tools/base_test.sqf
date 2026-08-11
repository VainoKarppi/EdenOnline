// !!! WARNING !!!
//
// DO NOT CHANGE THE "XXXX" PART!
// THIS IS AUTOMATICALLY UPDATED TO YOUR EXTENSION NAME BASED ON ASSEMBLY NAME!
//
// !!!!!!!!!!!!!!!

"XXXX" callExtension "version";

"XXXX" callExtension ["StartServer|1",[2302,"Razer","Altis","2.00.146773",["3db741e9"],""]];


//"XXXX" callExtension ["StartServer|1",[5000,"Razer","Altis", "2.00.146773",["3db741e9"], ""]];

//"XXXX" callExtension "TestNetwork";

sleep 1;

"XXXX" callExtension ["SetInitialMissionAttributes",[[["Scenario","Briefing",false],["NilValue","Test",nil], ["Multiplayer","Respawn",2]]]];


//"XXXX" callExtension ["CameraUpdate",[[0,0,0], [0,0,0]]];

//"XXXX" callExtension ["CameraUpdate|-1",[[4593.98,5088.45,10],[0.704759,0.687867,-0.173648]]];


//["CreateObject", [_id, _attributes]] call EXT_fnc_callExtensionAsync;

sleep 1;

//"XXXX" callExtension ["Disconnect|222",[]];

"XXXX" callExtension ["Connect|1",["84.250.208.125",2302,"Legodev","Altis","2.00.146773",["3db741e9"],""]];

sleep 1;


"XXXX" callExtension ["CameraUpdate|-1",[[4593.98,5088.45,10],[0.704759,0.687867,-0.173648]]];

sleep 1;

freeExtension "XXXX";
