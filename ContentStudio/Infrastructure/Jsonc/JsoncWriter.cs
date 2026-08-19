using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ContentStudio.Infrastructure.Jsonc;

/// <summary>Formatting facts detected from an existing file so inserted text blends in.</summary>
public sealed record JsoncStyle(string IndentUnit, string Newline)
{
    public static JsoncStyle Detect(string text)
    {
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";

        // The first indented line tells us the unit; the repo uses two spaces throughout.
        foreach (var line in EnumerateLines(text))
        {
            var leadingSpaces = 0;
            while (leadingSpaces < line.Length && line[leadingSpaces] == ' ')
                leadingSpaces++;
            if (leadingSpaces > 0 && leadingSpaces < line.Length)
                return new JsoncStyle(new string(' ', leadingSpaces), newline);
            if (line.Length > 0 && line[0] == '\t')
                return new JsoncStyle("\t", newline);
        }
        return new JsoncStyle("  ", newline);
    }

    private static IEnumerable<string> EnumerateLines(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
            yield return line;
    }
}

/// <summary>
/// Serializes <see cref="JsonNode"/> values in the two layouts the authored files use:
/// compact single-line records (<c>{ "id": "material.granite", "name": "Granite" }</c>) and
/// conventionally indented multi-line blocks. Unicode is written raw, matching hand-authored text.
/// </summary>
public static class JsoncWriter
{
    private static readonly JsonSerializerOptions RawTextOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string WriteSingleLine(JsonNode? value)
    {
        var builder = new StringBuilder();
        AppendSingleLine(builder, value);
        return builder.ToString();
    }

    public static string WriteMultiLine(JsonNode? value, JsoncStyle style, string baseIndent)
    {
        var builder = new StringBuilder();
        AppendMultiLine(builder, value, style, baseIndent);
        return builder.ToString();
    }

    private static void AppendSingleLine(StringBuilder builder, JsonNode? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject objectValue when objectValue.Count == 0:
                builder.Append("{}");
                break;
            case JsonObject objectValue:
                builder.Append("{ ");
                var firstMember = true;
                foreach (var (name, memberValue) in objectValue)
                {
                    if (!firstMember)
                        builder.Append(", ");
                    firstMember = false;
                    builder.Append(EncodeString(name)).Append(": ");
                    AppendSingleLine(builder, memberValue);
                }
                builder.Append(" }");
                break;
            case JsonArray arrayValue when arrayValue.Count == 0:
                builder.Append("[]");
                break;
            case JsonArray arrayValue:
                builder.Append('[');
                for (var index = 0; index < arrayValue.Count; index++)
                {
                    if (index > 0)
                        builder.Append(", ");
                    AppendSingleLine(builder, arrayValue[index]);
                }
                builder.Append(']');
                break;
            default:
                builder.Append(ScalarText(value));
                break;
        }
    }

    private static void AppendMultiLine(StringBuilder builder, JsonNode? value, JsoncStyle style, string indent)
    {
        switch (value)
        {
            case JsonObject objectValue when objectValue.Count > 0:
            {
                builder.Append('{').Append(style.Newline);
                var innerIndent = indent + style.IndentUnit;
                var index = 0;
                foreach (var (name, memberValue) in objectValue)
                {
                    builder.Append(innerIndent).Append(EncodeString(name)).Append(": ");
                    AppendValueChoosingLayout(builder, memberValue, style, innerIndent);
                    if (++index < objectValue.Count)
                        builder.Append(',');
                    builder.Append(style.Newline);
                }
                builder.Append(indent).Append('}');
                break;
            }
            case JsonArray arrayValue when arrayValue.Count > 0:
            {
                builder.Append('[').Append(style.Newline);
                var innerIndent = indent + style.IndentUnit;
                for (var index = 0; index < arrayValue.Count; index++)
                {
                    builder.Append(innerIndent);
                    AppendValueChoosingLayout(builder, arrayValue[index], style, innerIndent);
                    if (index < arrayValue.Count - 1)
                        builder.Append(',');
                    builder.Append(style.Newline);
                }
                builder.Append(indent).Append(']');
                break;
            }
            default:
                AppendSingleLine(builder, value);
                break;
        }
    }

    /// <summary>
    /// Small leaf containers stay on one line (the authored style for costs, packets and
    /// property maps); anything with nesting or many members breaks across lines.
    /// </summary>
    private static void AppendValueChoosingLayout(StringBuilder builder, JsonNode? value, JsoncStyle style, string indent)
    {
        if (IsCompactLeaf(value))
            AppendSingleLine(builder, value);
        else
            AppendMultiLine(builder, value, style, indent);
    }

    private static bool IsCompactLeaf(JsonNode? value) => value switch
    {
        JsonObject objectValue => objectValue.Count <= 4 && objectValue.All(member => member.Value is not (JsonObject or JsonArray)),
        JsonArray arrayValue => arrayValue.Count <= 6 && arrayValue.All(item => item is not (JsonObject or JsonArray)),
        _ => true,
    };

    public static string EncodeString(string value) => JsonSerializer.Serialize(value, RawTextOptions);

    public static string ScalarText(JsonNode value) => value.ToJsonString(RawTextOptions);
}
