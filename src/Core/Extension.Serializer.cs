using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ArmaExtension;

internal static class Serializer
{
    /// <summary>
    /// Deserialize an Arma array string into object[], supporting nested arrays.
    /// </summary>
    internal static object?[] DeserializeJsonArray(MethodInfo method, string[] armaString)
    {
        if (armaString.Length == 0) return [];

        var parameters = method.GetParameters();
        var result = new object?[armaString.Length];

        for (int i = 0; i < armaString.Length; i++)
        {
            string item = armaString[i];
            object? parsed;

            try
            {
                // Detect JSON array input
                if (item.StartsWith("[") && item.EndsWith("]"))
                {
                    var node = JsonNode.Parse(item);

                    if (node is JsonArray arr){
                        parsed = ConvertJsonArray(arr);

                        // 🔹 Convert to Dictionary if method expects it
                        if (i < parameters.Length && parameters[i].ParameterType == typeof(Dictionary<string, object?>) && parsed is object?[] array) {
                            parsed = ConvertArmaArrayToDictionary(array);
                        }
                    }
                    else parsed = null;
                }
                else
                {
                    TryParseSingleArmaItem(item, out parsed);
                }
            }
            catch
            {
                parsed = item;
            }

            result[i] = parsed;
        }

        return result;
    }

    private static Dictionary<string, object?> ConvertArmaArrayToDictionary(object?[] array)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var item in array)
        {
            // Format: [["key1", value1], ["key2", value2], ...]
            if (item is object?[] pair && pair.Length == 2 && pair[0] is string key) {
                dict[key] = pair[1];
            }
        }

        return dict;
    }

    internal static object? ConvertJsonArray(JsonArray jsonArray)
    {
        var temp = new object?[jsonArray.Count];

        for (int i = 0; i < jsonArray.Count; i++)
        {
            var item = jsonArray[i];
            if (item is JsonArray nested)
            {
                temp[i] = ConvertJsonArray(nested); // recursive array
            }
            else if (item is JsonValue val)
            {
                TryParseSingleArmaItem(val.ToString(), out temp[i]); // immediately parse value
            }
            else
            {
                temp[i] = null;
            }
        }

        return temp; // return only primitive values or object[]
    }

    internal static void TryParseSingleArmaItem(string item, out object? result)
    {
        // Preserve exact empty string
        if (item == "") {
            result = "";
            return;
        }

        result = null;

        if (item is null) return;

        var trimmed = item.Trim();
        var lower = trimmed.ToLowerInvariant();

        // Null-like values (Arma semantics)
        if (lower == "nil" || lower == "any" || lower == "nan" || lower == "objnull") {
            result = null;
            return;
        }

        if (int.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out int i)) {
            result = i;
            return;
        }

        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)) {
            result = d;
            return;
        }

        if (bool.TryParse(trimmed, out bool b)) {
            result = b;
            return;
        }

        // Quoted string (from JSON-like input)
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"') {
            result = trimmed[1..^1];
            return;
        }

        // Fallback string (DO NOT null this)
        result = trimmed;
        return;
    }

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

    /// <summary>
    /// Prepares the parameter array for method invocation.
    /// Truncates extra arguments, validates required parameters, and fills optional defaults.
    /// </summary>
    internal static object?[] PrepareMethodParameters(MethodInfo method, object?[] unserializedData, int? asyncKey = null)
    {
        ParameterInfo[] parameters = method.GetParameters();
        int requiredCount = parameters.Count(p => !p.IsOptional);

        // Truncate extra arguments
        if (unserializedData.Length > parameters.Length)
            unserializedData = unserializedData.Take(parameters.Length).ToArray();

        // Check minimum required parameters
        if (unserializedData.Length < requiredCount)
        {
            if (asyncKey.HasValue)
                throw new ArmaAsyncException(asyncKey.Value,
                    $"Parameter count mismatch for method {method.Name}. Expected at least {requiredCount} ({parameters.Length} total, {parameters.Length - requiredCount} optional), got {unserializedData.Length}.");
            else
                throw new ArmaException(
                    $"Parameter count mismatch for method {method.Name}. Expected at least {requiredCount} ({parameters.Length} total, {parameters.Length - requiredCount} optional), got {unserializedData.Length}.");
        }

        // Fill missing optional parameters with defaults
        if (unserializedData.Length < parameters.Length)
        {
            object?[] extended = new object?[parameters.Length];
            Array.Copy(unserializedData, extended, unserializedData.Length);

            for (int i = unserializedData.Length; i < parameters.Length; i++)
                extended[i] = parameters[i].DefaultValue;

            unserializedData = extended;
        }

        return unserializedData;
    }
}