using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;
using ContentStudio.Models;
using Dungeons.Combat;

namespace ContentStudio.Services;

/// <summary>
/// Builds editor field schemas by reflecting the Core definition classes — the same classes
/// <c>DataStore&lt;T&gt;</c> deserializes into, so the generated schema can never disagree with
/// what the game actually reads. <see cref="SchemaOverrides"/> then layers on the knowledge
/// reflection cannot see: which strings are references, which dictionary keys come from which
/// vocabulary, and friendlier labels.
/// </summary>
public static class SchemaGenerator
{
    private const int MaxNestingDepth = 7;

    public static IReadOnlyList<FieldSchema> GenerateFor(Type definitionType) =>
        GenerateFields(definitionType, depth: 0);

    private static IReadOnlyList<FieldSchema> GenerateFields(Type type, int depth)
    {
        var fields = new List<FieldSchema>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                continue;
            if (property.SetMethod is null) // computed properties are never authored
                continue;

            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                           ?? ToCamelCase(property.Name);
            var schema = DescribeValue(property.PropertyType, jsonName, depth);
            fields.Add(SchemaOverrides.Apply(type, schema));
        }
        return fields;
    }

    private static FieldSchema DescribeValue(Type valueType, string jsonName, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(valueType);
        if (underlying is not null)
            return DescribeValue(underlying, jsonName, depth) with { Optional = true };

        var label = Humanize(jsonName);

        if (valueType == typeof(string))
            return new FieldSchema { Name = jsonName, Label = label, Kind = "string" };
        if (valueType == typeof(bool))
            return new FieldSchema { Name = jsonName, Label = label, Kind = "bool" };
        if (valueType == typeof(int) || valueType == typeof(long))
            return new FieldSchema { Name = jsonName, Label = label, Kind = "int" };
        if (valueType == typeof(double) || valueType == typeof(float))
            return new FieldSchema { Name = jsonName, Label = label, Kind = "number" };

        if (valueType.IsEnum)
        {
            return new FieldSchema
            {
                Name = jsonName,
                Label = label,
                Kind = "enum",
                EnumValues = EnumAuthoringValues(valueType),
            };
        }

        if (valueType == typeof(MoveGrantSpec))
            return new FieldSchema { Name = jsonName, Label = label, Kind = "moveGrant", RefTypes = new[] { "moves" } };

        if (TryGetDictionaryValueType(valueType, out var dictionaryValueType))
            return DescribeDictionary(dictionaryValueType, jsonName, label, depth);

        if (TryGetListElementType(valueType, out var elementType))
            return DescribeList(elementType, jsonName, label, depth);

        if (depth >= MaxNestingDepth)
            return new FieldSchema { Name = jsonName, Label = label, Kind = "json" };

        if (valueType.IsClass || valueType is { IsValueType: true, IsPrimitive: false })
        {
            return new FieldSchema
            {
                Name = jsonName,
                Label = label,
                Kind = "object",
                Fields = GenerateFields(valueType, depth + 1),
            };
        }

        return new FieldSchema { Name = jsonName, Label = label, Kind = "json" };
    }

    private static FieldSchema DescribeList(Type elementType, string jsonName, string label, int depth)
    {
        if (elementType == typeof(string))
        {
            var kind = jsonName.Equals("tags", StringComparison.OrdinalIgnoreCase) ? "tags" : "stringList";
            return new FieldSchema { Name = jsonName, Label = label, Kind = kind };
        }
        if (elementType == typeof(int) || elementType == typeof(long) || elementType == typeof(double))
            return new FieldSchema { Name = jsonName, Label = label, Kind = "numberList" };
        if (elementType == typeof(MoveGrantSpec))
            return new FieldSchema { Name = jsonName, Label = label, Kind = "moveGrantList", RefTypes = new[] { "moves" } };
        if (elementType.IsEnum)
        {
            return new FieldSchema
            {
                Name = jsonName, Label = label, Kind = "stringList",
                EnumValues = EnumAuthoringValues(elementType),
            };
        }

        return new FieldSchema
        {
            Name = jsonName,
            Label = label,
            Kind = "objectList",
            Fields = depth >= MaxNestingDepth ? null : GenerateFields(elementType, depth + 1),
        };
    }

    private static FieldSchema DescribeDictionary(Type dictionaryValueType, string jsonName, string label, int depth)
    {
        if (dictionaryValueType == typeof(double) || dictionaryValueType == typeof(int))
            return new FieldSchema { Name = jsonName, Label = label, Kind = "numberDict" };

        if (TryGetListElementType(dictionaryValueType, out var listElement))
        {
            return new FieldSchema
            {
                Name = jsonName,
                Label = label,
                Kind = "objectListDict",
                Fields = depth >= MaxNestingDepth ? null : GenerateFields(listElement, depth + 1),
            };
        }

        return new FieldSchema
        {
            Name = jsonName,
            Label = label,
            Kind = "objectDict",
            Fields = depth >= MaxNestingDepth ? null : GenerateFields(dictionaryValueType, depth + 1),
        };
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        foreach (var candidate in EnumerateSelfAndInterfaces(type))
        {
            if (!candidate.IsGenericType)
                continue;
            var genericDefinition = candidate.GetGenericTypeDefinition();
            if ((genericDefinition == typeof(IDictionary<,>) || genericDefinition == typeof(IReadOnlyDictionary<,>)) &&
                candidate.GetGenericArguments()[0] == typeof(string))
            {
                valueType = candidate.GetGenericArguments()[1];
                return true;
            }
        }
        valueType = typeof(object);
        return false;
    }

    private static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(object);
            return false;
        }
        foreach (var candidate in EnumerateSelfAndInterfaces(type))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&
                typeof(IEnumerable).IsAssignableFrom(type))
            {
                elementType = candidate.GetGenericArguments()[0];
                return true;
            }
        }
        elementType = typeof(object);
        return false;
    }

    private static IEnumerable<Type> EnumerateSelfAndInterfaces(Type type)
    {
        yield return type;
        foreach (var implemented in type.GetInterfaces())
            yield return implemented;
    }

    /// <summary>The value list an editor should offer for an enum, in the game's authored spelling.</summary>
    private static IReadOnlyList<string> EnumAuthoringValues(Type enumType)
    {
        // ModifierKind is authored lowercase with underscores (its converter says so).
        if (enumType == typeof(Dungeons.Modifiers.ModifierKind))
            return new[] { "additive", "multiplicative", "flag", "diminishing", "highest_only" };
        return Enum.GetNames(enumType);
    }

    private static string ToCamelCase(string propertyName) =>
        propertyName.Length == 0 ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    /// <summary>Turns a JSON key into a readable label: <c>baseIntervalTicks</c> → "Base Interval Ticks".</summary>
    public static string Humanize(string jsonName)
    {
        var characters = new List<char>(jsonName.Length + 8);
        var previousWasLower = false;
        foreach (var character in jsonName)
        {
            if (character is '_' or '-')
            {
                characters.Add(' ');
                previousWasLower = false;
                continue;
            }
            if (char.IsUpper(character) && previousWasLower)
                characters.Add(' ');
            previousWasLower = char.IsLower(character);
            characters.Add(character);
        }
        var spaced = new string(characters.ToArray());
        return string.Join(' ', spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
