using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArmaExtension;



public static class Serializer {
    /// <summary>
    /// Unserializes data from Arma into an object[].
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns>object array that contains all the data</returns>
    public static object?[] DeserializeJsonArray(string[] armaString) {

        if (armaString.Length == 0) return [];

        List<object?> result = [];

        if (armaString.Length > 0) {
            foreach (var item in armaString) {
                
                try {
                    JsonNode? node = JsonNode.Parse(item);  // Attempt to parse the string as JSON
                    if (node == null) throw new Exception();

                    if (node is JsonArray jsonArray) {
                        result.Add(ConvertJsonArray(jsonArray));  // Handle array and recursively convert
                    } else if (node is JsonValue value) {
                        // Use TryParseSingleArmaItem to parse the value
                        if (!TryParseSingleArmaItem(value.ToString(), out object? parsedValue)) {
                            parsedValue = null;  // If parsing fails, set to null (or handle as needed)
                        }
                        result.Add(parsedValue);
                    } else {
                        result.Add(null);  // If it's something else (shouldn't happen with valid JSON)
                    }
                } catch (JsonException) {
                    // If parsing fails (invalid JSON), treat it as a regular value
                    if (!TryParseSingleArmaItem(item, out object? parsedItem)) {
                        parsedItem = null;  // If parsing fails, set to null (or handle as needed)
                    }
                    result.Add(parsedItem);
                }
            }
        }

        return result.ToArray();  // Return the final result array
    }
    private static object?[] ConvertJsonArray(JsonArray jsonArray) {
        object?[] result = new object[jsonArray.Count];
        for (int i = 0; i < jsonArray.Count; i++) {
            JsonNode? item = jsonArray[i];
            if (item is JsonArray nestedArray) {
                result[i] = ConvertJsonArray(nestedArray);  // Recursively process nested arrays
            } else if (item is JsonValue value) {
                // Call TryParseSingleArmaItem and check the result
                if (!TryParseSingleArmaItem(value.ToString(), out result[i])) {
                    result[i] = null!;  // If parsing fails, set to null (or handle it as needed)
                }
            } else {
                result[i] = null!;  // Handle any other unexpected cases
            }
        }
        return result;
    }
    public static string PrintArray(object?[]? array) {
        if (array is null) return "[]";

        return "[" + string.Join(",", array.Select(item =>
            item switch {
                object[] nested => PrintArray(nested),
                bool b => b.ToString().ToLower(),
                string str => @$"""{str}""",
                _ => item?.ToString() ?? "null"
            })) + "]";
    }
    

    // TODO can never return false
    public static bool TryParseSingleArmaItem(string item, out object? result) {
        // Try parsing a boolean
        if (bool.TryParse(item, out bool b)) {
            result = b;
            return true;
        }

        // Try parsing a double (numeric value)
        if (double.TryParse(item, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)) {
            result = d;
            return true;
        }

        // Check if it's a quoted string
        if (item.StartsWith('"') && item.EndsWith('"')) {
            result = item.Trim('"');
            return true;
        }

        // Handle nulls
        if (item.ToLower() == "nil" || item.ToLower() == "any" || item.ToLower() == "nan" || item.ToLower() == "objNull") {
            result = null;
            return true;
        }

        result = item;  // If nothing else matches, return the original string
        return true;
    }


    //TODO: Implement this
    private static string ParseDictionaryToArma(Dictionary<object, object> dict) {
        List<string> pairs = [];
        foreach (var kvp in dict) {
            string key = kvp.Key is string ? $"'{kvp.Key}'" : kvp.Key.ToString()!;
            string value = kvp.Value is string ? $"'{kvp.Value}'" : kvp.Value.ToString()!;
            pairs.Add($"{key},{value}");
        }
        return $"[{string.Join(",", pairs)}]";
    }
}
