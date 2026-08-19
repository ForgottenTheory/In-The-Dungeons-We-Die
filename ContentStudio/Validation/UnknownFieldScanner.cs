using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ContentStudio.Models;
using Dungeons.Combat;

namespace ContentStudio.Validation;

/// <summary>
/// Finds JSON fields the game will silently ignore. <c>DataStore&lt;T&gt;</c> matches keys
/// case-insensitively against the C# property name — or against the exact
/// <c>[JsonPropertyName]</c> when one is declared — and drops everything else without a word.
/// A typo like <c>moveId</c> where the type declares <c>move_id</c> therefore "loads fine"
/// and simply does nothing; this scanner is what turns that silence into a warning.
/// </summary>
public static class UnknownFieldScanner
{
    private sealed class ShapeNode
    {
        /// <summary>Known JSON key (lowercased) → the shape of that member's value.</summary>
        public Dictionary<string, ShapeNode?> Members { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>For dictionaries: keys are free; values share one shape.</summary>
        public ShapeNode? DictionaryValueShape { get; set; }

        /// <summary>For lists: the element shape.</summary>
        public ShapeNode? ListElementShape { get; set; }

        /// <summary>MoveGrantSpec accepts either a bare string or an object.</summary>
        public bool AllowsBareString { get; set; }
    }

    private static readonly Dictionary<Type, ShapeNode?> ShapeCache = new();

    public static void Scan(ContentRecordState record, Type definitionType, List<ValidationProblem> problems)
    {
        var shape = ShapeFor(definitionType, depth: 0);
        if (shape is null)
            return;
        ScanValue(record.Value, shape, record, path: "", problems);
    }

    private static void ScanValue(JsonNode? value, ShapeNode shape, ContentRecordState record, string path, List<ValidationProblem> problems)
    {
        switch (value)
        {
            case JsonObject objectValue when shape.DictionaryValueShape is not null:
                foreach (var (key, member) in objectValue)
                {
                    if (member is not null)
                        ScanValue(member, shape.DictionaryValueShape, record, $"{path}.{key}", problems);
                }
                break;

            case JsonObject objectValue when shape.Members.Count > 0:
                foreach (var (key, member) in objectValue)
                {
                    if (!shape.Members.TryGetValue(key, out var memberShape))
                    {
                        problems.Add(new ValidationProblem("warning", "studio", record.TypeId,
                            $"{record.Id}: unknown field \"{JoinPath(path, key)}\" — the game silently ignores it" + NearestKnownHint(key, shape),
                            record.Id, record.TypeId, record.File.RelativePath));
                        continue;
                    }
                    if (memberShape is not null && member is not null)
                        ScanValue(member, memberShape, record, JoinPath(path, key), problems);
                }
                break;

            case JsonArray arrayValue when shape.ListElementShape is not null:
                for (var index = 0; index < arrayValue.Count; index++)
                {
                    if (arrayValue[index] is { } element)
                        ScanValue(element, shape.ListElementShape, record, $"{path}[{index}]", problems);
                }
                break;
        }
    }

    private static string JoinPath(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    private static string NearestKnownHint(string unknownKey, ShapeNode shape)
    {
        foreach (var known in shape.Members.Keys)
        {
            if (Normalize(known) == Normalize(unknownKey))
                return $" (did you mean \"{known}\"?)";
        }
        return "";
    }

    private static string Normalize(string key) => key.Replace("_", "").ToLowerInvariant();

    private static ShapeNode? ShapeFor(Type type, int depth)
    {
        if (depth > 8)
            return null;
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return ShapeFor(underlying, depth);

        if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal))
            return null;

        if (ShapeCache.TryGetValue(type, out var cached))
            return cached;

        var shape = BuildShape(type, depth);
        ShapeCache[type] = shape;
        return shape;
    }

    private static ShapeNode? BuildShape(Type type, int depth)
    {
        if (type == typeof(MoveGrantSpec))
        {
            var grantShape = new ShapeNode { AllowsBareString = true };
            grantShape.Members["id"] = null;
            grantShape.Members["replaces"] = null;
            return grantShape;
        }

        foreach (var candidate in new[] { type }.Concat(type.GetInterfaces()))
        {
            if (!candidate.IsGenericType)
                continue;
            var definition = candidate.GetGenericTypeDefinition();
            if ((definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>)) &&
                candidate.GetGenericArguments()[0] == typeof(string))
            {
                return new ShapeNode { DictionaryValueShape = ShapeFor(candidate.GetGenericArguments()[1], depth + 1) ?? Anything() };
            }
        }
        foreach (var candidate in new[] { type }.Concat(type.GetInterfaces()))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>) && type != typeof(string))
            {
                var element = ShapeFor(candidate.GetGenericArguments()[0], depth + 1);
                return element is null ? null : new ShapeNode { ListElementShape = element };
            }
        }

        if (!type.IsClass && type is not { IsValueType: true, IsPrimitive: false })
            return null;

        var shape = new ShapeNode();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null || property.SetMethod is null)
                continue;
            // The declared [JsonPropertyName] REPLACES the C# name — exactly like System.Text.Json.
            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
            shape.Members[jsonName] = ShapeFor(property.PropertyType, depth + 1);
        }
        return shape.Members.Count == 0 ? null : shape;
    }

    /// <summary>A dictionary of unknown value shape: keys free, values unchecked.</summary>
    private static ShapeNode Anything() => new();
}
