
using System.Security.Cryptography;
using System.Text;

namespace EdenOnline;

public static class HashUtils
{
    public static string GetHash(object item)
    {
        // Flatten the object into a string deterministically
        string str = FlattenObject(item);
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        byte[] hash = SHA256.HashData(bytes);

        // Convert to hex string
        StringBuilder sb = new();
        foreach (byte b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string FlattenObject(object obj)
    {
        if (obj == null) return "null";

        // Handle arrays
        if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
        {
            StringBuilder sb = new();
            sb.Append("[");
            bool first = true;
            foreach (var element in enumerable)
            {
                if (!first) sb.Append(",");
                sb.Append(FlattenObject(element));
                first = false;
            }
            sb.Append("]");
            return sb.ToString();
        }

        // Primitive types
        return obj.ToString() ?? "";
    }
}