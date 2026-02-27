using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using static ArmaExtension.Logger;
using static ArmaExtension.Enums;
using static ArmaExtension.MethodSystem;
using static ArmaExtension.Events;

namespace ArmaExtension;

internal static class Serializer
{
    /// <summary>
    /// Deserialize an Arma array string into object[], supporting nested arrays.
    /// </summary>
    internal static object?[] DeserializeJsonArray(string[] armaString)
    {
        if (armaString.Length == 0) return [];

        var result = new object?[armaString.Length];

        for (int i = 0; i < armaString.Length; i++)
        {
            string item = armaString[i];
            object? parsed;

            try
            {
                if (item.StartsWith("[") && item.EndsWith("]"))
                {
                    var node = JsonNode.Parse(item);
                    parsed = node is JsonArray arr ? ConvertJsonArray(arr) : null;
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

    internal static bool TryParseSingleArmaItem(string item, out object? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(item)) return true;

        var trimmed = item.Trim();
        var lower = trimmed.ToLower();

        // Null-like values
        if (lower == "nil" || lower == "any" || lower == "nan" || lower == "objnull")
            return true;

        // Integer
        if (int.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out int i))
        {
            result = i;
            return true;
        }

        // Double
        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
        {
            result = d;
            return true;
        }

        // Boolean
        if (bool.TryParse(trimmed, out bool b))
        {
            result = b;
            return true;
        }

        // Quoted string
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            result = trimmed[1..^1];
            return true;
        }

        // Fallback string
        result = trimmed;
        return true;
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

    internal static string PrintItem(object? item) => item switch
    {
        object[] arr => PrintArray(arr),
        bool b => b.ToString().ToLower(),
        string str => $"\"{str}\"",
        null => "nil",
        _ => Convert.ToString(item, CultureInfo.InvariantCulture)!
    };

    /// <summary>
    /// Prepares the parameter array for method invocation.
    /// Truncates extra arguments, validates required parameters, and fills optional defaults.
    /// </summary>
    internal static object?[] PrepareMethodParameters(MethodInfo method, object?[] unserializedData, int? asyncKey)
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