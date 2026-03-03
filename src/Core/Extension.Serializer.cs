using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ArmaExtension;

internal static class Serializer
{
    #region ARMA TO C#
    internal static object?[] DeserializeJsonArray(MethodInfo method, string[] armaString, int? asyncKey = null) {
        var parameters = method.GetParameters();
        int requiredCount = parameters.Count(p => !p.IsOptional);

        // Validate minimum required parameter count
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

        // Fill optional parameters
        for (int i = finalCount; i < parameters.Length; i++)
            result[i] = parameters[i].DefaultValue;

        return result;
    }

    private static object? DeserializeToType(string input, Type targetType) {
        if (input == "")
            return targetType == typeof(string) ? "" : null;

        var trimmed = input.Trim();

        // Arma null semantics
        if (trimmed.Equals("nil", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("any", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("nan", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("objnull", StringComparison.OrdinalIgnoreCase))
            return null;

        // Nullable<T>
        if (Nullable.GetUnderlyingType(targetType) is Type inner)
            return DeserializeToType(trimmed, inner);

        // JSON Array
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) {
            var node = JsonNode.Parse(trimmed);
            if (node is JsonArray arr)
                return ConvertArray(arr, targetType);

            return null;
        }

        // Strings
        if (targetType == typeof(string) || targetType == typeof(object)) {
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
                return trimmed[1..^1];

            return trimmed;
        }

        // Enums
        if (targetType.IsEnum)
            return Enum.Parse(targetType, trimmed, true);

        // Primitives
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
        // Dictionary<string, object?>
        if (targetType == typeof(Dictionary<string, object?>)) {
            var dict = new Dictionary<string, object?>();

            foreach (var item in array) {
                if (item is JsonArray pair && pair.Count == 2 && pair[0]?.GetValue<string>() is string key) {
                    dict[key] = pair[1] switch {
                        JsonArray nested => ConvertArray(nested, typeof(object[])),
                        JsonValue val => DeserializeToType(val.ToString(), typeof(object)),
                        _ => null
                    };
                }
            }

            return dict;
        }

        // Typed array
        if (targetType.IsArray) {
            var elementType = targetType.GetElementType()!;
            var typed = Array.CreateInstance(elementType, array.Count);

            for (int i = 0; i < array.Count; i++) {
                object? value = array[i] switch {
                    JsonArray nested => ConvertArray(nested, elementType),
                    JsonValue val => DeserializeToType(val.ToString(), elementType),
                    _ => null
                };

                typed.SetValue(value, i);
            }

            return typed;
        }

        // Fallback → object[]
        var fallback = new object?[array.Count];

        for (int i = 0; i < array.Count; i++) {
            fallback[i] = array[i] switch {
                JsonArray nested => ConvertArray(nested, typeof(object[])),
                JsonValue val => DeserializeToType(val.ToString(), typeof(object)),
                _ => null
            };
        }

        return fallback;
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