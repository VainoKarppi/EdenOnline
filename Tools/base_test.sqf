// !!! WARNING !!!
//
// DO NOT CHANGE THE "XXXX" PART!
// THIS IS AUTOMATICALLY UPDATED TO YOUR EXTENSION NAME BASED ON ASSEMBLY NAME!
//
// !!!!!!!!!!!!!!!

"XXXX" callExtension "version";


"XXXX" callExtension ["StartServer|999",["PLAYER NAME",5000,"Altis", "2.00.146773",["3db741e9","7a3fd23a","46981d1a","322ac721","9f4e6826","32cfcb0d","eb442c52","8b4af371","82d83132","8152a08e","6a67d97a","f9bd46ab","640e771a"], ""]];


//"XXXX" callExtension "TestNetwork";

sleep 1;
// string objectID, string classname, object[] position, object[] rotation, string parentId, string groupId
"XXXX" callExtension ["CreateObject",["AAA",[["Name",""],["Init",""]]]];


sleep 5;
"XXXX" callExtension "Disconnect";

sleep 2;
freeExtension "XXXX";