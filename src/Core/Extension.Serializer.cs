using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ArmaExtension;

internal static class Serializer
{

    // TODO
    /*
        0:49:42.746 | [Debug] ARMA >> EXTENSION (Args) >> 2245589715904 "OI5AZBN9",[["ItemClass","B_support_Mort_F"],["Name",""],["Init",""],["Pylons",<null>],["Position",[647.483,2341.57,0]],["Rotation",[0,0,0]],["Size3",[0,0,0]],["IsRectangle",false],["PlacementRadius",0],["ControlSP",true],["ControlMP",false],["Description",""],["Lock",1],["Skill",0.5],["Health",1],["Fuel",1],["Ammo",1],["Rank","PRIVATE"],["UnitPos",3],["DynamicSimulation",false],["AddToDynSimGrid",true],["EnableSimulation",true],["ObjectIsSimple",false],["IsLocalOnly",false],["allowDamage",true],["DoorStates",[0,0,0]],["EnableRevive",false],["hideObject",false],["enableStamina",true],["NameSound",""],["speaker","male01eng"],["pitch",0.960855],["unitName","Alfie Hall"],["unitInsignia",""],["face","GreekHead_A3_07"],["Presence",1],["PresenceCondition","true"],["ammoBox","[[[[],[]],[[],[]],[[],[]],[[],[]]],false]"],["VehicleCustomization",[[],[]]],["ReportRemoteTargets",false],["ReceiveRemoteTargets",false],["ReportOwnPosition",false],["RadarUsageAI",<null>]]
        20:49:42.746 | [Debug] EXTENSION >> ARMA >> (CreateObject) >> ["ASYNC_SENT",[]]
        20:49:42.748 | [Debug] EXTENSION CALLBACK >> ARMA >> ["ArmaExtension", "ASYNC_SENT_FAILED|83656|1", "["'<' is an invalid start of a value. LineNumber: 0 | BytePositionInLine: 68."]"]
        20:49:42.748 | [Debug] AsyncTaskCompleted event triggered with method: CreateObject, asyncKey: 83656, success: False, response count: 1
    */
    #region ARMA TO C#
    internal static object?[] DeserializeJsonArray(MethodInfo method, string[] armaString, int? asyncKey = null) {
        var parameters = method.GetParameters();
        int requiredCount = parameters.Count(p => !p.IsOptional);

        if (armaString.Length < requiredCount)
            ThrowParamCount(method, armaString.Length, requiredCount, parameters.Length, asyncKey);

        int finalCount = Math.Min(armaString.Length, parameters.Length);

        object?[] result = new object?[parameters.Length];

        for (int i = 0; i < finalCount; i++) {
            var param = parameters[i];
            var expectedType = param.ParameterType;
            var raw = armaString[i]?.Trim() ?? "";

            bool isArrayInput = raw.StartsWith("[");
            bool expectsArray = expectedType.IsArray || expectedType == typeof(Dictionary<string, object?>);

            if (isArrayInput && !expectsArray)
                ThrowTypeMismatch(method, param, i, "array", expectedType.Name, asyncKey);

            if (!isArrayInput && expectsArray)
                ThrowTypeMismatch(method, param, i, "scalar", expectedType.Name, asyncKey);

            result[i] = DeserializeToType(raw, expectedType);
        }

        for (int i = finalCount; i < parameters.Length; i++)
            result[i] = parameters[i].DefaultValue;

        return result;
    }


    private static string NormalizeArmaJson(string input) {

        if (string.IsNullOrWhiteSpace(input))
            return input;

        var span = input.AsSpan();

        if (!span.Contains("nil", StringComparison.OrdinalIgnoreCase) &&
            !span.Contains("any", StringComparison.OrdinalIgnoreCase) &&
            !span.Contains("nan", StringComparison.OrdinalIgnoreCase) &&
            !span.Contains("objnull", StringComparison.OrdinalIgnoreCase) &&
            !span.Contains("<null", StringComparison.OrdinalIgnoreCase))
            return input;

        string result = input;

        result = result.Replace("nil", "null", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("any", "null", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("nan", "null", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("objnull", "null", StringComparison.OrdinalIgnoreCase);

        while (true) {
            int start = result.IndexOf("<null", StringComparison.OrdinalIgnoreCase);
            if (start == -1) break;

            int end = result.IndexOf('>', start);
            if (end == -1) break;

            result = result.Remove(start, end - start + 1)
                        .Insert(start, "null");
        }

        return result;
    }
    private static object? DeserializeToType(string input, Type targetType) {

        if (input == "")
            return targetType == typeof(string) ? "" : null;

        var trimmed = input.Trim();

        // Arma null semantics
        if (trimmed.Contains("<null-", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("<null -", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("nil", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("any", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("nan", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("objnull", StringComparison.OrdinalIgnoreCase))
            return null;

        if (Nullable.GetUnderlyingType(targetType) is Type inner)
            return DeserializeToType(trimmed, inner);

        if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) {

            var normalized = NormalizeArmaJson(trimmed);

            var node = JsonNode.Parse(normalized);
            if (node is JsonArray arr)
                return ConvertArray(arr, targetType);

            return null;
        }

        if (targetType == typeof(string) || targetType == typeof(object)) {
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
                return trimmed[1..^1];

            return trimmed;
        }

        if (targetType.IsEnum)
            return Enum.Parse(targetType, trimmed, true);

        if (targetType == typeof(int))
            return int.Parse(trimmed, CultureInfo.InvariantCulture);

        if (targetType == typeof(long))
            return long.Parse(trimmed, CultureInfo.InvariantCulture);

        if (targetType == typeof(float))
            return float.Parse(trimmed, CultureInfo.InvariantCulture);

        if (targetType == typeof(double))
            return double.Parse(trimmed, CultureInfo.InvariantCulture);

        if (targetType == typeof(bool))
            return bool.Parse(trimmed);

        return Convert.ChangeType(trimmed, targetType, CultureInfo.InvariantCulture);
    }

    private static object? ConvertArray(JsonArray array, Type targetType) {

        if (targetType == typeof(Dictionary<string, object?>)) {
            var dict = new Dictionary<string, object?>();

            foreach (var item in array) {
                if (item is JsonArray pair && pair.Count == 2 &&
                    pair[0] is JsonValue keyVal &&
                    keyVal.TryGetValue<string>(out var key)) {

                    dict[key] = pair[1] switch {
                        JsonArray nested => ConvertArray(nested, typeof(object[])),
                        JsonValue val => ExtractJsonValue(val),
                        _ => null
                    };
                }
            }

            return dict;
        }

        if (targetType.IsArray) {
            var elementType = targetType.GetElementType()!;
            var typed = Array.CreateInstance(elementType, array.Count);

            for (int i = 0; i < array.Count; i++) {
                object? value = array[i] switch {
                    JsonArray nested => ConvertArray(nested, elementType),
                    JsonValue val => ExtractJsonValue(val),
                    _ => null
                };

                typed.SetValue(value, i);
            }

            return typed;
        }

        var fallback = new object?[array.Count];

        for (int i = 0; i < array.Count; i++) {
            fallback[i] = array[i] switch {
                JsonArray nested => ConvertArray(nested, typeof(object[])),
                JsonValue val => ExtractJsonValue(val),
                _ => null
            };
        }

        return fallback;
    }

    private static object? ExtractJsonValue(JsonValue val) {

        if (val.TryGetValue<string>(out var s)) return s;
        if (val.TryGetValue<int>(out var i)) return i;
        if (val.TryGetValue<long>(out var l)) return l;
        if (val.TryGetValue<double>(out var d)) return d;
        if (val.TryGetValue<bool>(out var b)) return b;

        return null;
    }

    private static void ThrowTypeMismatch(MethodInfo method, ParameterInfo param, int index, string received, string expected, int? asyncKey) {
        string message =
            $"Type mismatch in method '{method.Name}' at parameter '{param.Name}' (index {index}). " +
            $"Expected {expected}, but received {received}.";

        if (asyncKey.HasValue) throw new ArmaAsyncException(asyncKey.Value, message);

        throw new ArmaException(message);
    }

    private static void ThrowParamCount(MethodInfo method, int got, int required, int total, int? asyncKey) {
        string message =
            $"Parameter count mismatch for method '{method.Name}'. " +
            $"Expected at least {required} ({total} total), got {got}.";

        if (asyncKey.HasValue) throw new ArmaAsyncException(asyncKey.Value, message);

        throw new ArmaException(message);
    }

    #endregion

    #region C# TO ARMA
    /// <summary>
    /// Convert object array into Arma-style array string.
    /// Supports nested arrays.
    /// </summary>
    internal static string PrintArray(object?[]? array)
    {
        if (array == null) return "[]";
        return "[" + string.Join(",", array.Select(PrintItem)) + "]";
    }

    private static string PrintItem(object? item) => item switch
    {
        null => "nil",
        bool b => b.ToString().ToLower(),
        string s => $"\"{s}\"",
        object[] arr => PrintArray(arr),
        IDictionary<string, object?> dict => PrintDictionary(dict),
        IEnumerable<object?> list => PrintArray(list.ToArray()),
        _ => Convert.ToString(item, CultureInfo.InvariantCulture)!
    };

    // Converts Dictionary<string, object?> to Arma array format
    private static string PrintDictionary(IDictionary<string, object?> dict)
    {
        if (dict.Count == 0) return "[]";

        var items = dict.Select(kvp =>
            $"[\"{kvp.Key}\",{PrintItem(kvp.Value)}]"
        );

        return "[" + string.Join(",", items) + "]";
    }
    #endregion
}