using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ContentStudio.Infrastructure.Jsonc;

/// <summary>One replacement of a source span with new text. Produced by <see cref="JsoncPatcher"/>.</summary>
public readonly record struct TextEdit(int Start, int End, string NewText);

/// <summary>
/// Turns "the record should now look like this JSON value" into the smallest set of text edits
/// against the original JSONC source. Untouched members keep their exact bytes, which is what
/// keeps the authored files' comments and formatting alive across edits. Wholesale
/// re-serialization is the last resort, used only for shapes a targeted edit cannot express.
/// </summary>
public static class JsoncPatcher
{
    // ── Public entry points ─────────────────────────────────────────────────────────────────

    public static string ApplyEdits(string sourceText, IReadOnlyList<TextEdit> edits)
    {
        var ordered = edits.OrderByDescending(edit => edit.Start).ToList();
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].End > ordered[index - 1].Start)
                throw new InvalidOperationException("Overlapping text edits — this is a patcher bug.");
        }

        var builder = new StringBuilder(sourceText);
        foreach (var edit in ordered)
        {
            builder.Remove(edit.Start, edit.End - edit.Start);
            builder.Insert(edit.Start, edit.NewText);
        }
        return builder.ToString();
    }

    /// <summary>Edits that morph <paramref name="current"/> into <paramref name="desired"/>.</summary>
    public static List<TextEdit> ComputeValueEdits(string sourceText, JsoncNode current, JsonNode? desired, JsoncStyle style)
    {
        var edits = new List<TextEdit>();
        DiffValue(sourceText, current, desired, style, edits);
        return edits;
    }

    /// <summary>Appends a new element to an array (a new record in an array-of-records file).</summary>
    public static TextEdit AppendArrayElement(string sourceText, JsoncArray array, JsonNode element, JsoncStyle style)
    {
        var serialized = ElementsAreSingleLine(sourceText, array)
            ? JsoncWriter.WriteSingleLine(element)
            : JsoncWriter.WriteMultiLine(element, style, ItemIndent(sourceText, array, style));
        return InsertIntoContainer(sourceText, array, array.Items.LastOrDefault(), serialized, style,
            ItemIndent(sourceText, array, style));
    }

    /// <summary>Inserts a new element directly after an existing one, so a duplicated record
    /// lands beside its source instead of at the end of the file.</summary>
    public static TextEdit InsertArrayElementAfter(string sourceText, JsoncArray array, int afterIndex, JsonNode element, JsoncStyle style)
    {
        if (afterIndex >= array.Items.Count - 1)
            return AppendArrayElement(sourceText, array, element, style);

        var anchor = array.Items[afterIndex];
        var itemIndent = ItemIndent(sourceText, array, style);
        var serialized = ElementsAreSingleLine(sourceText, array)
            ? JsoncWriter.WriteSingleLine(element)
            : JsoncWriter.WriteMultiLine(element, style, itemIndent);

        var insertAt = PositionAfterFollowingComma(sourceText, anchor.End);
        var containerIsMultiLine = sourceText.AsSpan(array.Start, array.End - array.Start).Contains('\n');
        return containerIsMultiLine
            ? new TextEdit(insertAt, insertAt, $"{style.Newline}{itemIndent}{serialized},")
            : new TextEdit(insertAt, insertAt, $" {serialized},");
    }

    /// <summary>
    /// Removes one array element (one record), including the comma and any comment lines that
    /// sit directly on top of it. A comment block separated by a blank line is treated as a
    /// section header and survives.
    /// </summary>
    public static TextEdit RemoveArrayElement(string sourceText, JsoncArray array, int index)
    {
        var item = array.Items[index];
        int deleteStart, deleteEnd;

        if (index < array.Items.Count - 1)
        {
            deleteStart = IncludeAttachedLeadingTrivia(sourceText, item.Start);
            deleteEnd = PositionAfterFollowingComma(sourceText, item.End);
        }
        else if (index > 0)
        {
            // Last element: delete from the comma after the previous element through this value,
            // which also removes comment lines attached to the deleted element.
            deleteStart = PositionOfFollowingComma(sourceText, array.Items[index - 1].End);
            deleteEnd = item.End;
        }
        else
        {
            deleteStart = IncludeAttachedLeadingTrivia(sourceText, item.Start);
            deleteEnd = item.End;
        }

        (deleteStart, deleteEnd) = TidyDeletedLines(sourceText, deleteStart, deleteEnd);
        return new TextEdit(deleteStart, deleteEnd, string.Empty);
    }

    // ── Recursive diff ──────────────────────────────────────────────────────────────────────

    private static void DiffValue(string sourceText, JsoncNode current, JsonNode? desired, JsoncStyle style, List<TextEdit> edits)
    {
        switch (current)
        {
            case JsoncObject currentObject when desired is JsonObject desiredObject:
                DiffObject(sourceText, currentObject, desiredObject, style, edits);
                return;
            case JsoncArray currentArray when desired is JsonArray desiredArray:
                DiffArray(sourceText, currentArray, desiredArray, style, edits);
                return;
            case JsoncScalar currentScalar when desired is not (JsonObject or JsonArray):
                if (!ScalarEquals(currentScalar, desired))
                    edits.Add(new TextEdit(currentScalar.Start, currentScalar.End, SerializeValueAt(sourceText, currentScalar.Start, desired, style)));
                return;
            default:
                // The value changed shape (scalar ↔ container); replace the whole span.
                edits.Add(new TextEdit(current.Start, current.End, SerializeValueAt(sourceText, current.Start, desired, style)));
                return;
        }
    }

    private static void DiffObject(string sourceText, JsoncObject current, JsonObject desired, JsoncStyle style, List<TextEdit> edits)
    {
        var desiredNames = new HashSet<string>(desired.Select(member => member.Key), StringComparer.Ordinal);

        foreach (var member in current.Members)
        {
            if (!desiredNames.Contains(member.Name))
                edits.Add(RemoveMember(sourceText, current, member));
            else
                DiffValue(sourceText, member.Value, desired[member.Name], style, edits);
        }

        var currentNames = new HashSet<string>(current.Members.Select(member => member.Name), StringComparer.Ordinal);
        var memberIndent = MemberIndent(sourceText, current, style);
        foreach (var (name, value) in desired)
        {
            if (currentNames.Contains(name))
                continue;
            var serialized = $"{JsoncWriter.EncodeString(name)}: {SerializeForContainer(sourceText, current, value, style, memberIndent)}";
            edits.Add(InsertIntoContainer(sourceText, current, current.Members.LastOrDefault()?.Value, serialized, style, memberIndent));
        }
    }

    private static void DiffArray(string sourceText, JsoncArray current, JsonArray desired, JsoncStyle style, List<TextEdit> edits)
    {
        if (DeepEquals(current, desired))
            return;

        if (desired.Count == current.Items.Count)
        {
            for (var index = 0; index < desired.Count; index++)
                DiffValue(sourceText, current.Items[index], desired[index], style, edits);
            return;
        }

        if (desired.Count > current.Items.Count)
        {
            for (var index = 0; index < current.Items.Count; index++)
                DiffValue(sourceText, current.Items[index], desired[index], style, edits);
            var itemIndent = ItemIndent(sourceText, current, style);
            for (var index = current.Items.Count; index < desired.Count; index++)
            {
                var serialized = SerializeForContainer(sourceText, current, desired[index], style, itemIndent);
                edits.Add(InsertIntoContainer(sourceText, current, current.Items.LastOrDefault(), serialized, style, itemIndent));
            }
            return;
        }

        // Fewer elements than before: if the survivors are an in-order subsequence, delete the
        // dropped ones surgically; anything more tangled (reorders) rewrites the array while
        // reusing the original text of elements that moved unchanged.
        if (TryDeleteAsSubsequence(sourceText, current, desired, style, edits))
            return;

        edits.Add(RewriteArray(sourceText, current, desired, style));
    }

    private static bool TryDeleteAsSubsequence(string sourceText, JsoncArray current, JsonArray desired, JsoncStyle style, List<TextEdit> edits)
    {
        var matchedCurrentIndices = new List<int>();
        var searchFrom = 0;
        foreach (var desiredItem in desired)
        {
            var found = -1;
            for (var index = searchFrom; index < current.Items.Count; index++)
            {
                if (DeepEquals(current.Items[index], desiredItem) || SameRecordId(current.Items[index], desiredItem))
                {
                    found = index;
                    break;
                }
            }
            if (found < 0)
                return false;
            matchedCurrentIndices.Add(found);
            searchFrom = found + 1;
        }

        var matched = new HashSet<int>(matchedCurrentIndices);
        for (var index = current.Items.Count - 1; index >= 0; index--)
        {
            if (!matched.Contains(index))
                edits.Add(RemoveArrayElement(sourceText, current, index));
        }
        for (var position = 0; position < matchedCurrentIndices.Count; position++)
            DiffValue(sourceText, current.Items[matchedCurrentIndices[position]], desired[position], style, edits);
        return true;
    }

    /// <summary>
    /// Rebuilds the whole array. Elements that match an original element (deep-equal, or the
    /// same record <c>id</c>) carry their original source text — comments inside them move with
    /// them, which is what makes drag-reordering safe in comment-heavy files.
    /// </summary>
    private static TextEdit RewriteArray(string sourceText, JsoncArray current, JsonArray desired, JsoncStyle style)
    {
        var singleLineElements = ElementsAreSingleLine(sourceText, current);
        var multiLineArray = sourceText.AsSpan(current.Start, current.End - current.Start).Contains('\n');
        var itemIndent = ItemIndent(sourceText, current, style);
        var closeIndent = LineIndentAt(sourceText, current.Start);

        var availableOriginals = new List<JsoncNode>(current.Items);
        var pieces = new List<string>();
        foreach (var desiredItem in desired)
        {
            var reused = availableOriginals.FirstOrDefault(original => DeepEquals(original, desiredItem));
            if (reused is null)
            {
                var idMatch = availableOriginals.FirstOrDefault(original => SameRecordId(original, desiredItem));
                if (idMatch is not null && desiredItem is not null)
                {
                    // Same record, edited during the reorder: patch its own text, then move it.
                    var elementText = sourceText[idMatch.Start..idMatch.End];
                    var reparsed = JsoncParser.Parse(elementText);
                    var innerEdits = ComputeValueEdits(elementText, reparsed, desiredItem, style);
                    pieces.Add(ApplyEdits(elementText, innerEdits));
                    availableOriginals.Remove(idMatch);
                    continue;
                }
                pieces.Add(singleLineElements
                    ? JsoncWriter.WriteSingleLine(desiredItem)
                    : JsoncWriter.WriteMultiLine(desiredItem, style, itemIndent));
                continue;
            }
            pieces.Add(sourceText[reused.Start..reused.End]);
            availableOriginals.Remove(reused);
        }

        string rebuilt;
        if (!multiLineArray)
        {
            rebuilt = pieces.Count == 0 ? "[]" : $"[{string.Join(", ", pieces)}]";
        }
        else
        {
            var builder = new StringBuilder("[").Append(style.Newline);
            for (var index = 0; index < pieces.Count; index++)
            {
                builder.Append(itemIndent).Append(pieces[index]);
                if (index < pieces.Count - 1)
                    builder.Append(',');
                builder.Append(style.Newline);
            }
            builder.Append(closeIndent).Append(']');
            rebuilt = builder.ToString();
        }
        return new TextEdit(current.Start, current.End, rebuilt);
    }

    // ── Member/element surgery helpers ──────────────────────────────────────────────────────

    private static TextEdit RemoveMember(string sourceText, JsoncObject parent, JsoncMember member)
    {
        var memberIndex = parent.Members.IndexOf(member);
        int deleteStart, deleteEnd;

        if (memberIndex < parent.Members.Count - 1)
        {
            deleteStart = IncludeAttachedLeadingTrivia(sourceText, member.NameStart);
            deleteEnd = PositionAfterFollowingComma(sourceText, member.Value.End);
        }
        else if (memberIndex > 0)
        {
            deleteStart = PositionOfFollowingComma(sourceText, parent.Members[memberIndex - 1].Value.End);
            deleteEnd = member.Value.End;
        }
        else
        {
            deleteStart = IncludeAttachedLeadingTrivia(sourceText, member.NameStart);
            deleteEnd = member.Value.End;
        }

        (deleteStart, deleteEnd) = TidyDeletedLines(sourceText, deleteStart, deleteEnd);
        return new TextEdit(deleteStart, deleteEnd, string.Empty);
    }

    private static TextEdit InsertIntoContainer(
        string sourceText, JsoncNode container, JsoncNode? lastExistingValue, string serialized, JsoncStyle style, string innerIndent)
    {
        var containerIsMultiLine = sourceText.AsSpan(container.Start, container.End - container.Start).Contains('\n');

        if (lastExistingValue is null)
        {
            var insertAt = container.Start + 1;
            return containerIsMultiLine
                ? new TextEdit(insertAt, insertAt, $"{style.Newline}{innerIndent}{serialized}")
                : new TextEdit(insertAt, insertAt, container is JsoncObject ? $" {serialized} " : serialized);
        }

        // Respect an existing trailing comma (legal in these files) — never emit a second one.
        var afterLast = lastExistingValue.End;
        var scan = afterLast;
        while (scan < sourceText.Length && sourceText[scan] is ' ' or '\t')
            scan++;
        var hasTrailingComma = scan < sourceText.Length && sourceText[scan] == ',';

        if (hasTrailingComma)
        {
            var insertAt = scan + 1;
            return containerIsMultiLine
                ? new TextEdit(insertAt, insertAt, $"{style.Newline}{innerIndent}{serialized},")
                : new TextEdit(insertAt, insertAt, $" {serialized},");
        }

        return containerIsMultiLine
            ? new TextEdit(afterLast, afterLast, $",{style.Newline}{innerIndent}{serialized}")
            : new TextEdit(afterLast, afterLast, $", {serialized}");
    }

    /// <summary>Extends a deletion upward over comment lines that sit directly on the element,
    /// stopping at a blank line (section headers stay) or at non-comment content.</summary>
    private static int IncludeAttachedLeadingTrivia(string sourceText, int elementStart)
    {
        var lineStart = LineStartOf(sourceText, elementStart);
        if (!IsWhitespace(sourceText, lineStart, elementStart))
            return elementStart; // shares a line with something else — touch nothing extra

        var start = lineStart;
        while (start > 0)
        {
            var previousLineStart = LineStartOf(sourceText, start - 1 - (start >= 2 && sourceText[start - 2] == '\r' ? 1 : 0));
            var previousLine = sourceText[previousLineStart..start];
            var trimmed = previousLine.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                start = previousLineStart;
                continue;
            }
            break;
        }
        return start;
    }

    private static (int Start, int End) TidyDeletedLines(string sourceText, int deleteStart, int deleteEnd)
    {
        // If the deletion leaves an empty line behind (whitespace before it on the line, and
        // whitespace-then-newline after), swallow the leading whitespace and the newline too.
        var lineStart = LineStartOf(sourceText, deleteStart);
        if (!IsWhitespace(sourceText, lineStart, deleteStart))
            return (deleteStart, deleteEnd);

        var scan = deleteEnd;
        while (scan < sourceText.Length && sourceText[scan] is ' ' or '\t')
            scan++;
        if (scan < sourceText.Length && sourceText[scan] == '\r')
            scan++;
        if (scan < sourceText.Length && sourceText[scan] == '\n')
            return (lineStart, scan + 1);
        return (deleteStart, deleteEnd);
    }

    private static int PositionOfFollowingComma(string sourceText, int fromPosition)
    {
        var position = fromPosition;
        JsoncParser.SkipTrivia(sourceText, ref position);
        return position < sourceText.Length && sourceText[position] == ','
            ? position
            : fromPosition; // no comma found (shouldn't happen for a non-only element) — degrade gracefully
    }

    private static int PositionAfterFollowingComma(string sourceText, int fromPosition)
    {
        var commaPosition = PositionOfFollowingComma(sourceText, fromPosition);
        return commaPosition < sourceText.Length && sourceText[commaPosition] == ',' ? commaPosition + 1 : fromPosition;
    }

    // ── Layout probes ───────────────────────────────────────────────────────────────────────

    private static string SerializeValueAt(string sourceText, int position, JsonNode? value, JsoncStyle style)
    {
        if (value is not (JsonObject or JsonArray))
            return value is null ? "null" : JsoncWriter.ScalarText(value);
        var indent = LineIndentAt(sourceText, position);
        return JsoncWriter.WriteMultiLine(value, style, indent);
    }

    private static string SerializeForContainer(string sourceText, JsoncNode container, JsonNode? value, JsoncStyle style, string innerIndent)
    {
        var containerIsMultiLine = sourceText.AsSpan(container.Start, container.End - container.Start).Contains('\n');
        if (!containerIsMultiLine)
            return JsoncWriter.WriteSingleLine(value);
        return value is JsonObject or JsonArray
            ? JsoncWriter.WriteMultiLine(value, style, innerIndent)
            : JsoncWriter.WriteSingleLine(value);
    }

    private static bool ElementsAreSingleLine(string sourceText, JsoncArray array)
    {
        if (array.Items.Count == 0)
            return false;
        var singleLineCount = array.Items.Count(item => !sourceText.AsSpan(item.Start, item.End - item.Start).Contains('\n'));
        return singleLineCount * 2 >= array.Items.Count;
    }

    private static string MemberIndent(string sourceText, JsoncObject objectNode, JsoncStyle style) =>
        objectNode.Members.Count > 0
            ? LineIndentAt(sourceText, objectNode.Members[0].NameStart)
            : LineIndentAt(sourceText, objectNode.Start) + style.IndentUnit;

    private static string ItemIndent(string sourceText, JsoncArray arrayNode, JsoncStyle style) =>
        arrayNode.Items.Count > 0
            ? LineIndentAt(sourceText, arrayNode.Items[0].Start)
            : LineIndentAt(sourceText, arrayNode.Start) + style.IndentUnit;

    private static string LineIndentAt(string sourceText, int position)
    {
        var lineStart = LineStartOf(sourceText, position);
        var scan = lineStart;
        while (scan < sourceText.Length && sourceText[scan] is ' ' or '\t')
            scan++;
        return sourceText[lineStart..scan];
    }

    private static int LineStartOf(string sourceText, int position)
    {
        var scan = Math.Min(position, sourceText.Length);
        while (scan > 0 && sourceText[scan - 1] != '\n')
            scan--;
        return scan;
    }

    private static bool IsWhitespace(string sourceText, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (sourceText[index] is not (' ' or '\t'))
                return false;
        }
        return true;
    }

    // ── Value comparison ────────────────────────────────────────────────────────────────────

    public static bool DeepEquals(JsoncNode current, JsonNode? desired) => (current, desired) switch
    {
        (JsoncObject currentObject, JsonObject desiredObject) =>
            currentObject.Members.Count == desiredObject.Count &&
            currentObject.Members.All(member =>
                desiredObject.TryGetPropertyValue(member.Name, out var desiredValue) && DeepEquals(member.Value, desiredValue)),
        (JsoncArray currentArray, JsonArray desiredArray) =>
            currentArray.Items.Count == desiredArray.Count &&
            currentArray.Items.Zip(desiredArray).All(pair => DeepEquals(pair.First, pair.Second)),
        (JsoncScalar currentScalar, _) when desired is not (JsonObject or JsonArray) => ScalarEquals(currentScalar, desired),
        _ => false,
    };

    private static bool ScalarEquals(JsoncScalar current, JsonNode? desired)
    {
        if (desired is null)
            return current.Kind == JsonValueKind.Null;
        var desiredElement = JsonSerializer.SerializeToElement(desired);
        return current.Kind switch
        {
            JsonValueKind.String when desiredElement.ValueKind == JsonValueKind.String =>
                JsoncParser.DecodeStringScalar(current) == desiredElement.GetString(),
            // Compare numerically so "5.0" vs 5 (or "0.50" vs 0.5) never registers as an edit.
            JsonValueKind.Number when desiredElement.ValueKind == JsonValueKind.Number =>
                double.Parse(current.RawText, CultureInfo.InvariantCulture) == desiredElement.GetDouble(),
            JsonValueKind.True => desiredElement.ValueKind == JsonValueKind.True,
            JsonValueKind.False => desiredElement.ValueKind == JsonValueKind.False,
            JsonValueKind.Null => desiredElement.ValueKind == JsonValueKind.Null,
            _ => false,
        };
    }

    private static bool SameRecordId(JsoncNode current, JsonNode? desired)
    {
        if (current is not JsoncObject currentObject || desired is not JsonObject desiredObject)
            return false;
        var currentId = currentObject.FindMember("id")?.Value as JsoncScalar;
        if (currentId is null || currentId.Kind != JsonValueKind.String)
            return false;
        return desiredObject.TryGetPropertyValue("id", out var desiredId) &&
               desiredId is JsonValue desiredValue &&
               desiredValue.TryGetValue<string>(out var desiredText) &&
               JsoncParser.DecodeStringScalar(currentId) == desiredText;
    }

    /// <summary>Converts a span tree into a plain value model for the API/UI layer.</summary>
    public static JsonNode? ToJsonNode(JsoncNode node) => node switch
    {
        JsoncObject objectNode => new JsonObject(objectNode.Members.Select(member =>
            new KeyValuePair<string, JsonNode?>(member.Name, ToJsonNode(member.Value)))),
        JsoncArray arrayNode => new JsonArray(arrayNode.Items.Select(ToJsonNode).ToArray()),
        JsoncScalar scalar => scalar.Kind switch
        {
            JsonValueKind.String => JsonValue.Create(JsoncParser.DecodeStringScalar(scalar)),
            JsonValueKind.Number => CreateNumberNode(scalar.RawText),
            JsonValueKind.True => JsonValue.Create(true),
            JsonValueKind.False => JsonValue.Create(false),
            _ => null,
        },
        _ => null,
    };

    private static JsonNode CreateNumberNode(string rawText) =>
        !rawText.Contains('.') && !rawText.Contains('e') && !rawText.Contains('E') && long.TryParse(rawText, out var integer)
            ? JsonValue.Create(integer)
            : JsonValue.Create(double.Parse(rawText, CultureInfo.InvariantCulture));
}
