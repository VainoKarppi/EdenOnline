using System;
using System.Collections.Generic;
using System.Text;

namespace ArmaExtension;

public static partial class EdenOnline
{
    public static class Serializer
    {
        // Native AOT-safe: serialize everything to a "loose" object array
        public static string SerializeParameters(object[] parameters)
        {
            var sb = new StringBuilder();
            sb.Append('[');

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');

                var p = parameters[i];

                switch (p)
                {
                    case string s:
                        sb.Append('"').Append(s.Replace("\"", "\\\"")).Append('"');
                        break;

                    case int n:
                    case long l:
                    case double d:
                    case float f:
                    case bool b:
                        sb.Append(Convert.ToString(p, System.Globalization.CultureInfo.InvariantCulture));
                        break;

                    case string[] arr:
                        sb.Append('[');
                        for (int j = 0; j < arr.Length; j++)
                        {
                            if (j > 0) sb.Append(',');
                            sb.Append('"').Append(arr[j].Replace("\"", "\\\"")).Append('"');
                        }
                        sb.Append(']');
                        break;

                    case object[] nested:
                        sb.Append(SerializeParameters(nested));
                        break;

                    default:
                        // fallback: everything else as string
                        sb.Append('"').Append(p?.ToString()?.Replace("\"", "\\\"") ?? "").Append('"');
                        break;
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        // AOT-safe parser: everything as object (numbers are parsed as double, strings as string)
        public static object[] DeserializeParameters(string input)
        {
            var results = new List<object>();
            int i = 0;

            SkipWhitespace(input, ref i);
            if (input[i++] != '[') throw new FormatException("Expected '['");

            while (true)
            {
                SkipWhitespace(input, ref i);
                if (input[i] == ']') { i++; break; }

                object value;
                if (input[i] == '"') value = ReadString(input, ref i);
                else if (input[i] == '[') value = DeserializeParameters(ReadArrayString(input, ref i));
                else value = ReadNumberOrBool(input, ref i);

                results.Add(value);

                SkipWhitespace(input, ref i);
                if (input[i] == ',') { i++; continue; }
                if (input[i] == ']') { i++; break; }
            }

            return results.ToArray();
        }

        // Helper parsing methods
        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            if (s[i++] != '"') throw new FormatException("Expected '\"'");
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '\\') sb.Append(s[i++]);
                else if (c == '"') break;
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string ReadArrayString(string s, ref int i)
        {
            int start = i;
            int depth = 0;
            do
            {
                if (s[i] == '[') depth++;
                else if (s[i] == ']') depth--;
                i++;
            } while (i < s.Length && depth > 0);

            return s[start..i];
        }

        private static object ReadNumberOrBool(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && !",]".Contains(s[i])) i++;
            string token = s[start..i].Trim();

            if (int.TryParse(token, out var intResult)) return intResult;
            if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleResult)) return doubleResult;
            if (bool.TryParse(token, out var boolResult)) return boolResult;

            return token; // fallback as string
        }
    }
}