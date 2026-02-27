// !!! WARNING !!!
//
// DO NOT CHANGE THE "XXXX" PART!
// THIS IS AUTOMATICALLY UPDATED TO YOUR EXTENSION NAME BASED ON ASSEMBLY NAME!
//
// !!!!!!!!!!!!!!!

"XXXX" callExtension "version";

// AVAILABLE CORE CALL FEATURES
"XXXX" callExtension ["MethodThatDoesNotExist",[]]

// This will throw error
"XXXX" callExtension "AsyncTest"
"XXXX" callExtension "AsyncReturnTest"

// Returns list of available methods in the extension, and data types: [["Version",[["ip","String",false],["port","Number",false]],"String",false], [...]] : [methodName, inputParameterTypes, outputType, asyncRequired]
//"XXXX" callExtension "GET_AVAILABLE_METHODS";

// RETURNS EITHER: ["ASYNC_CANCEL_SUCCESS",returnCode, errorCode] OR ["ASYNC_CANCEL_SUCCESS", returnCode, errorCode]
//"XXXX" callExtension "ASYNC_CANCEL|9999";

// RETURNS EITHER: ["[""ASYNC_STATUS_RUNNING"",[]]",returnCode, errorCode] OR ["[""ASYNC_STATUS_NOT_FOUND"",[]]", returnCode, errorCode]
//"XXXX" callExtension "ASYNC_STATUS|9999";



//"XXXX" callExtension "TestNetwork";

//"XXXX" callExtension ["GetHash",[123123]]
//"XXXX" callExtension ["AsyncReturnTest|2",[]]

//"XXXX" callExtension "ASYNC_STATUS|2"

//"XXXX" callExtension ["StartServer",["test",5000,"Altis", "2.00.146773",["ACE3", "3.16.0"], ""]];


//"XXXX" callExtension ["CreateObject",["ASD9999", "Land_Cargo20_yellow_F", [100,100,100], [0,180,0], 1]];

sleep 5;

"XXXX" callExtension "ASYNC_STATUS|2"

"XXXX" callExtension "Disconnect";

sleep 2;
freeExtension "XXXX";