using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Dungeons.Content;

/// <summary>
/// Generic, engine-independent registry of data-driven definitions keyed by id.
/// It parses JSON <em>text</em> supplied by the caller and never touches the file
/// system — the Godot layer owns <c>res://</c>/<c>user://</c> access and hands the
/// raw text in. This keeps content loading testable and reusable
/// (see docs/architecture.md §10, docs/json-schema.md §20).
/// </summary>
/// <typeparam name="T">The definition type; must expose a stable id.</typeparam>
public sealed class DataStore<T> where T : IDefinition
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
        // A JSON key that matches no property is a load error, never a silent shrug. Two
        // shipped records ("moveId" on a MoveMatch, "scalesWith" on an EffectSpec) were
        // quietly ignored for a whole milestone because this defaulted to Skip — the field
        // read as authored and the behaviour was the default's. Types with custom converters
        // (e.g. MoveGrantSpec) keep their own contracts; this covers everything reflection
        // deserializes.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                static typeInfo =>
                {
                    if (typeInfo.Kind == JsonTypeInfoKind.Object)
                        typeInfo.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                },
            },
        },
    };

    private readonly Dictionary<string, T> _byId = new(StringComparer.Ordinal);
    private readonly JsonSerializerOptions _options;

    public DataStore(JsonSerializerOptions? options = null)
    {
        _options = options ?? DefaultOptions;
    }

    /// <summary>Number of loaded definitions.</summary>
    public int Count => _byId.Count;

    /// <summary>Adds a single already-constructed definition.</summary>
    /// <exception cref="ArgumentException">If the id is null or whitespace.</exception>
    /// <exception cref="DuplicateDefinitionException">If the id is already present.</exception>
    public void Add(T definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id))
            throw new ArgumentException($"{typeof(T).Name} has a null or empty id.", nameof(definition));
        if (_byId.ContainsKey(definition.Id))
            throw new DuplicateDefinitionException(definition.Id, typeof(T));

        _byId.Add(definition.Id, definition);
    }

    /// <summary>Parses one JSON object into a definition and registers it.</summary>
    public T LoadOne(string json)
    {
        var definition = Deserialize(json)
            ?? throw new JsonException($"JSON deserialized to null for {typeof(T).Name}.");
        Add(definition);
        return definition;
    }

    /// <summary>Parses a JSON array of definitions and registers each in order.</summary>
    public IReadOnlyList<T> LoadMany(string json)
    {
        var items = JsonSerializer.Deserialize<List<T>>(json, _options)
            ?? throw new JsonException($"JSON deserialized to null for {typeof(T).Name}[].");
        foreach (var item in items)
            Add(item);
        return items;
    }

    /// <summary>
    /// Loads a set of JSON documents, each of which may be a single object <em>or</em> an
    /// array of objects (auto-detected per document). This lets a content type be authored
    /// either one-per-file or grouped into array files (e.g. materials by category) without
    /// the caller caring which — file access still lives on the Godot/test side.
    /// </summary>
    public void LoadDocuments(IEnumerable<string> jsonDocuments)
    {
        ArgumentNullException.ThrowIfNull(jsonDocuments);
        foreach (var json in jsonDocuments)
        {
            if (StartsWithArray(json))
                LoadMany(json);
            else
                LoadOne(json);
        }
    }

    /// <summary>True if the first meaningful character (ignoring a BOM/whitespace) is '['.</summary>
    private static bool StartsWithArray(string json)
    {
        var trimmed = json.TrimStart('﻿', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith('[');
    }

    /// <summary>
    /// Clears the store and reloads it from a set of single-object JSON documents
    /// (one per content file). Development reload / hot-swap builds on this.
    /// </summary>
    public void Reload(IEnumerable<string> singleObjectJsonDocuments)
    {
        ArgumentNullException.ThrowIfNull(singleObjectJsonDocuments);
        _byId.Clear();
        foreach (var json in singleObjectJsonDocuments)
            LoadOne(json);
    }

    /// <summary>Removes all definitions.</summary>
    public void Clear() => _byId.Clear();

    public bool Contains(string id) => _byId.ContainsKey(id);

    public bool TryGetById(string id, out T definition) => _byId.TryGetValue(id, out definition!);

    /// <exception cref="KeyNotFoundException">If no definition has the given id.</exception>
    public T GetById(string id)
    {
        if (_byId.TryGetValue(id, out var definition))
            return definition;
        throw new KeyNotFoundException($"No {typeof(T).Name} with id '{id}'.");
    }

    /// <summary>All loaded definitions.</summary>
    public IReadOnlyCollection<T> GetAll() => _byId.Values;

    private T? Deserialize(string json) => JsonSerializer.Deserialize<T>(json, _options);
}
