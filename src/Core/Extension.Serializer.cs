using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using static EdenOnline.Logger;

namespace EdenOnline;

internal static class Serializer
{
    #region ARMA TO C#
    internal static object?[] DeserializeArmaArray(MethodInfo method, string[] armaString, int? asyncKey = null) {
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
            bool acceptsObjectPayload = expectedType == typeof(object);

            if (isArrayInput && !expectsArray && !acceptsObjectPayload)
                ThrowTypeMismatch(method, param, i, "array", expectedType.Name, asyncKey);

            if (!isArrayInput && expectsArray && !acceptsObjectPayload)
                ThrowTypeMismatch(method, param, i, "scalar", expectedType.Name, asyncKey);

            result[i] = DeserializeToType(raw, expectedType);
        }

        for (int i = finalCount; i < parameters.Length; i++)
            result[i] = parameters[i].DefaultValue;

        return result;
    }


    private static string NormalizeArmaJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var sb = new StringBuilder(input.Length);
        bool inString = false;

        for (int i = 0; i < input.Length;)
        {
            char c = input[i];

            if (c == '"')
            {
                inString = !inString;
                sb.Append(c);
                i++;
                continue;
            }

            if (!inString)
            {
                if (input.AsSpan(i).StartsWith("nil", StringComparison.OrdinalIgnoreCase) &&
                    IsTokenBoundary(input, i, 3))
                {
                    sb.Append("null");
                    i += 3;
                    continue;
                }

                if (input.AsSpan(i).StartsWith("any", StringComparison.OrdinalIgnoreCase) &&
                    IsTokenBoundary(input, i, 3))
                {
                    sb.Append("null");
                    i += 3;
                    continue;
                }

                if (input.AsSpan(i).StartsWith("nan", StringComparison.OrdinalIgnoreCase) &&
                    IsTokenBoundary(input, i, 3))
                {
                    sb.Append("null");
                    i += 3;
                    continue;
                }

                if (input.AsSpan(i).StartsWith("objnull", StringComparison.OrdinalIgnoreCase) &&
                    IsTokenBoundary(input, i, 7))
                {
                    sb.Append("null");
                    i += 7;
                    continue;
                }

                if (input.AsSpan(i).StartsWith("<null", StringComparison.OrdinalIgnoreCase))
                {
                    int end = input.IndexOf('>', i);

                    if (end != -1)
                    {
                        sb.Append("null");
                        i = end + 1;
                        continue;
                    }
                }
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();

        static bool IsTokenBoundary(string value, int start, int length)
        {
            bool startBoundary =
                start == 0 ||
                !char.IsLetterOrDigit(value[start - 1]) &&
                value[start - 1] != '_';

            int end = start + length;

            bool endBoundary =
                end >= value.Length ||
                !char.IsLetterOrDigit(value[end]) &&
                value[end] != '_';

            return startBoundary && endBoundary;
        }
    }
    private static object? DeserializeToType(string input, Type targetType) {
        if (input == "") return targetType == typeof(string) ? "" : null;

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

        if (targetType == typeof(object) && trimmed.ToLowerInvariant() == "false" || trimmed.ToLowerInvariant() == "true")
            return bool.Parse(trimmed);

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
        // Todo make support key
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

        var builder = new StringBuilder(Math.Max(2, array.Length * 4));
        AppendArray(builder, array);
        return builder.ToString();
    }

    private static void AppendArray(StringBuilder builder, IEnumerable<object?> array) {
        builder.Append('[');

        bool first = true;
        foreach (object? item in array) {
            if (!first) builder.Append(',');
            AppendItem(builder, item);
            first = false;
        }

        builder.Append(']');
    }

    private static void AppendItem(StringBuilder builder, object? item) {
        switch (item) {
            case null:
                builder.Append("nil");
                break;
            case bool value:
                builder.Append(value ? "true" : "false");
                break;
            case string value:
                builder.Append('"').Append(value).Append('"');
                break;
            case object?[] array:
                AppendArray(builder, array);
                break;
            case IDictionary<string, object?> dictionary:
                AppendDictionary(builder, dictionary);
                break;
            case IEnumerable<object?> list:
                AppendArray(builder, list);
                break;
            default:
                builder.Append(Convert.ToString(item, CultureInfo.InvariantCulture));
                break;
        }
    }

    // Converts Dictionary<string, object?> to Arma array format
    private static void AppendDictionary(StringBuilder builder, IDictionary<string, object?> dictionary) {
        builder.Append('[');

        bool first = true;
        foreach ((string key, object? value) in dictionary) {
            if (!first) builder.Append(',');
            builder.Append("[\"").Append(key).Append("\",");
            AppendItem(builder, value);
            builder.Append(']');
            first = false;
        }

        builder.Append(']');
    }
    #endregion
}
