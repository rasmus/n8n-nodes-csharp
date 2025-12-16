using System.Globalization;
using System.Text.Json.Nodes;

namespace N8n.CSharpRunner;

public static class JsonNodeExtensions
{
    public static JsonNode? Prop(this JsonNode? node, string propertyName)
    {
        return node?[propertyName];
    }

    public static string? Str(this JsonNode? node, string propertyName, string? defaultValue = null)
    {
        var valueNode = node?[propertyName];
        if (valueNode is null) return defaultValue;

        try
        {
            return valueNode.GetValue<string?>();
        }
        catch
        {
            // Fall back to JSON text representation (works for numbers/bools too)
            return valueNode.ToString();
        }
    }

    public static int? Int(this JsonNode? node, string propertyName, int? defaultValue = null)
    {
        var valueNode = node?[propertyName];
        if (valueNode is null) return defaultValue;

        try
        {
            return valueNode.GetValue<int>();
        }
        catch
        {
            if (int.TryParse(valueNode.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return defaultValue;
        }
    }

    public static bool? Bool(this JsonNode? node, string propertyName, bool? defaultValue = null)
    {
        var valueNode = node?[propertyName];
        if (valueNode is null) return defaultValue;

        try
        {
            return valueNode.GetValue<bool>();
        }
        catch
        {
            if (bool.TryParse(valueNode.ToString(), out var parsed))
            {
                return parsed;
            }
            return defaultValue;
        }
    }
}
