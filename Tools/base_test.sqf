// !!! WARNING !!!
//
// DO NOT CHANGE THE "XXXX" PART!
// THIS IS AUTOMATICALLY UPDATED TO YOUR EXTENSION NAME BASED ON ASSEMBLY NAME!
//
// !!!!!!!!!!!!!!!

"XXXX" callExtension "version";
//"XXXX" callExtension "TestNetwork";

"XXXX" callExtension ["GetHash",[123123]]
sleep 1
"XXXX" callExtension ["AsyncReturnTest|2",[]]

"XXXX" callExtension "ASYNC_STATUS|2"

//"XXXX" callExtension ["StartServer|10",["test",5000,"Altis", "2.00.146773",["ACE3", "3.16.0"], ""]];

sleep 1;


//"XXXX" callExtension ["CreateObject",["ASD9999", "Land_Cargo20_yellow_F", [100,100,100], [0,180,0], 1]];

sleep 5;

"XXXX" callExtension "ASYNC_STATUS|2"

"XXXX" callExtension "Disconnect";

sleep 2;
freeExtension "XXXX";