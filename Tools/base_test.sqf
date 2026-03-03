// !!! WARNING !!!
//
// DO NOT CHANGE THE "XXXX" PART!
// THIS IS AUTOMATICALLY UPDATED TO YOUR EXTENSION NAME BASED ON ASSEMBLY NAME!
//
// !!!!!!!!!!!!!!!

"XXXX" callExtension "version";


"XXXX" callExtension ["StartServer|999",["test",5000,"Altis", "2.00.146773",["ACE3", "3.16.0"], ""]];


//"XXXX" callExtension "TestNetwork";

sleep 1;
// string objectID, string classname, object[] position, object[] rotation, string parentId, string groupId
"XXXX" callExtension ["CreateObject",["AAA",[["ItemClass","B_Truck_01_mover_F"],["Name",""],["Init",""],["Pylons",""],["Position",[3106.45,1646.37,0]],["Rotation",[328.749,0,0]]]]];


sleep 5;
"XXXX" callExtension "Disconnect";

sleep 2;
freeExtension "XXXX";