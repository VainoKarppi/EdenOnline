// !!! WARNING !!!
//
// DO NOT CHANGE THE "XXXX" PART!
// THIS IS AUTOMATICALLY UPDATED TO YOUR EXTENSION NAME BASED ON ASSEMBLY NAME!
//
// !!!!!!!!!!!!!!!

"XXXX" callExtension "version";


"XXXX" callExtension ["Array|0",[10,[123],5]];
sleep 0.3;
"XXXX" callExtension ["ArrayInner|1",[[10,[123],5,["test",[2]]]]];
sleep 0.3;
"XXXX" callExtension ["Numeric|2",[10,10,10]];
sleep 0.3;
"XXXX" callExtension ["Boolean|3",[true]];
sleep 0.3;
"XXXX" callExtension ["String|5",["asdasd"]];
sleep 0.3;
"XXXX" callExtension ["Null|4",[nil]];


sleep 0.3;
"XXXX" callExtension ["Numeric",[10,10]];

sleep 0.3;
"XXXX" callExtension "NoArgs";
sleep 0.3;
"XXXX" callExtension "NoArgs|3333";
sleep 0.3;
"XXXX" callExtension "Numeric";
sleep 0.3;
"XXXX" callExtension ["String",["asdasd"]];

sleep 0.3;
freeExtension "XXXX";