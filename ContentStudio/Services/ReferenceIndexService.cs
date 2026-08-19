using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ContentStudio.Models;

namespace ContentStudio.Services;

/// <summary>One id-to-id reference found in authored content, with the field path it sits on.</summary>
public sealed record ReferenceEdge(string FromId, string ToId, string FieldPath);

/// <summary>
/// The project-wide reference graph, rebuilt from the actual JSON (never a hand-maintained
/// second database). Any string value that exactly matches an existing record id is an edge;
/// strings that merely *look* like ids of a known type but resolve to nothing are collected
/// as suspects for validation.
/// </summary>
public sealed partial class ReferenceIndexService
{
    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z0-9_]+)+$")]
    private static partial Regex IdShapedString();

    private List<ReferenceEdge> _edges = new();
    private Dictionary<string, List<ReferenceEdge>> _outgoingByRecord = new(StringComparer.Ordinal);
    private Dictionary<string, List<ReferenceEdge>> _incomingByRecord = new(StringComparer.Ordinal);
    private List<(ContentRecordState Record, string FieldPath, string UnresolvedId)> _unresolvedIdShapedStrings = new();

    public IReadOnlyList<ReferenceEdge> Edges => _edges;

    public IReadOnlyList<ReferenceEdge> OutgoingOf(string recordId) =>
        _outgoingByRecord.GetValueOrDefault(recordId) ?? (IReadOnlyList<ReferenceEdge>)Array.Empty<ReferenceEdge>();

    public IReadOnlyList<ReferenceEdge> IncomingOf(string recordId) =>
        _incomingByRecord.GetValueOrDefault(recordId) ?? (IReadOnlyList<ReferenceEdge>)Array.Empty<ReferenceEdge>();

    public IReadOnlyList<(ContentRecordState Record, string FieldPath, string UnresolvedId)> UnresolvedIdShapedStrings =>
        _unresolvedIdShapedStrings;

    public void Rebuild(ContentWorkspace workspace)
    {
        var allIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in workspace.AllRecords)
        {
            if (record.Id.Length > 0)
                allIds.Add(record.Id);
        }

        // Prefixes that mark a string as "meant to be a content id" even when it resolves to
        // nothing — the broken-reference suspects. Realm-scoped `loc.*` ids are deliberately
        // absent (they are not globally unique), as are bare-word ids like property names.
        var knownIdPrefixes = ContentTypeRegistry.All
            .Where(descriptor => descriptor.IdPrefix.Length > 0)
            .Select(descriptor => descriptor.IdPrefix)
            .ToList();

        var edges = new List<ReferenceEdge>();
        var unresolved = new List<(ContentRecordState, string, string)>();

        foreach (var record in workspace.AllRecords)
        {
            if (record.Id.Length == 0)
                continue;
            WalkStrings(record.Value, "", (fieldPath, text) =>
            {
                if (text == record.Id || !IdShapedString().IsMatch(text))
                    return;
                if (allIds.Contains(text))
                {
                    edges.Add(new ReferenceEdge(record.Id, text, fieldPath));
                    return;
                }
                if (knownIdPrefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal)))
                    unresolved.Add((record, fieldPath, text));
            });
        }

        _edges = edges;
        _unresolvedIdShapedStrings = unresolved;
        _outgoingByRecord = edges.GroupBy(edge => edge.FromId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        _incomingByRecord = edges.GroupBy(edge => edge.ToId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
    }

    private static void WalkStrings(JsonNode? node, string path, Action<string, string> onString)
    {
        switch (node)
        {
            case JsonObject objectNode:
                foreach (var (key, value) in objectNode)
                    WalkStrings(value, path.Length == 0 ? key : $"{path}.{key}", onString);
                break;
            case JsonArray arrayNode:
                for (var index = 0; index < arrayNode.Count; index++)
                    WalkStrings(arrayNode[index], $"{path}[{index}]", onString);
                break;
            case JsonValue value when value.TryGetValue<string>(out var text):
                onString(path, text);
                break;
        }
    }
}
