
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using ArmaExtension;
using static ArmaExtension.Logger;

namespace ArmaExtension; // Do not change the namespace, it is updated automatically by the build system.

// 

[ArmaExtensionPlugin]
public static class MyExtension {
    public static class ArmaMethods {
        // CALLED FROM ARMA USING:
        // _data = "ArmaExtension" callExtension "Version";
        // _data == "[""SUCCESS"",[""1.0.0.0""]]"
        public static string Version() {
            return Extension.Version;
        }

        // CALLED FROM ARMA USING:
        // _data = "ArmaExtension" callExtension ["Numeric",[10,10]];
        // _data == "[""SUCCESS"",[20]]"
        public static double Numeric(double first, double second) {
            Log($"Numeric Method Called: {first}+{second}");
            return first + second;
        }

        // IF YOU WANT TO USE ASYNC METHODS, YOU NEED TO USE THE ASYNC KEY
        // CALLED FROM ARMA USING:
        // _data = "ArmaExtension" callExtension ["Boolean|99999",[true]];
        // _data == "[""ASYNC_SENT"",CANCEL_TOKEN]"

        // You can then retrieve the result using:
        // addMissionEventHandler ["ExtensionCallback", { ... } Example in github

        public static object[] Boolean(bool input) {
            Log($"Boolean Method Called: {input}");
            return [true, 1000];
        }
        
        
        public static string String(string input)
        {
            Log($"String Method Called: {input}");
            return "IS THIS WORKINGgg";
        }
        public static void Null(bool input) {
            Log($"Null Method Called: {input}");
        }
        public static object[] Array(double first, object[] second, double third) {
            Log("Array Method Called");
            return [1, 2, 3, 4, 5];
        }
        public static object[] ArrayInner(object[] items) {
            Log("ArrayInner Method Called");
            return [1, 2, 3, 4, new object[] { 1 }, 5];
        }
        public static object[] NoArgs() {
            Log("NoArgs Method Called");
            return [1, 2, 3, 4, 5];
        }
    }



    // ! INITIALIZED WHEN FIRST EXTENSION CALL IS MADE
    // If just public static void is used in Main(), it will block the Arma 3 until this method is finished
    // If its using public static async Task, this will not block the Arma 3, but events might not have been registered yet.
    public static void Main()
    {
        Extension.RegisterMethods(typeof(ArmaMethods)); // Always register your methods

        // Subscribe to events
        Extension.VersionCalled += version => Log($"VersionCalled event triggered with version: {version}");
        Extension.MethodCalled += methodName => Log($"MethodCalled event triggered with method: {methodName}");

        Extension.MethodCalledWithArgs += (methodName, args) => Log($"MethodCalledWithArgs event: {methodName} with args: {args}");
        Extension.MethodCalledWithArgsResponse += (methodName, response, success) => Log($"MethodCalledWithArgsAndReturn event: {methodName} with response: {response}");

        Extension.AsyncTaskStarted += (method, asyncKey, args) => Log($"AsyncTaskStarted event triggered with method: {method}, asyncKey: {asyncKey}, args: {args}");
        Extension.AsyncTaskCompleted += (method, asyncKey, response, success) => Log($"AsyncTaskCompleted event triggered with method: {method}, asyncKey: {asyncKey}, success: {success}, response: {response}");
        Extension.AsyncTaskCancelled += (asyncKey, success) => Log($"AsyncTaskCancelled event triggered with asyncKey: {asyncKey}, success: {success},");

        Extension.OnSendToArma += (method, data) => Log($"OnSendToArma event triggered with method: {method}, data: {data},");

        Extension.ErrorOccurred += ex => Log($"ErrorOccurred event triggered: {ex}");


        // Send data to arma
        Extension.SendToArma("test_method1", [1, 2, 3]);
        Extension.SendToArma("test_method2", [true]);
    }
}