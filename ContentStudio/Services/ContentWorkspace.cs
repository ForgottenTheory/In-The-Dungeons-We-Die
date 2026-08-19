using System.Text.Json;
using System.Text.Json.Nodes;
using ContentStudio.Infrastructure;
using ContentStudio.Infrastructure.Jsonc;
using ContentStudio.Models;

namespace ContentStudio.Services;

/// <summary>
/// The in-memory model of every authored content file in the opened project, and the only
/// path through which Content Studio changes them. Edits are applied as surgical text patches
/// (comments survive), files stay unsaved until an explicit Save, and external changes are
/// reconciled rather than clobbered.
/// </summary>
public sealed class ContentWorkspace
{
    private readonly object _mutationLock = new();

    public string ProjectRoot { get; private set; } = "";
    public string DataDirectory { get; private set; } = "";
    public bool IsLoaded { get; private set; }

    private readonly Dictionary<string, ContentFileState> _filesByRelativePath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ContentRecordState> _recordsById = new(StringComparer.Ordinal);
    private readonly List<(string Id, ContentRecordState Record)> _duplicateIdRecords = new();

    /// <summary>Content types whose folder actually exists in the opened project.</summary>
    public List<ContentTypeDescriptor> PresentTypes { get; } = new();

    /// <summary>Raised after any mutation so validation, references and SSE stay current.</summary>
    public event Action? Changed;

    /// <summary>Raised when a file is reloaded from or flagged as conflicting with the disk.</summary>
    public event Action<ContentFileState, string>? FileStateChanged; // reason: "reloaded" | "conflict" | "saved"

    public object MutationLock => _mutationLock;

    // ── Loading ─────────────────────────────────────────────────────────────────────────────

    public void LoadProject(string projectRoot)
    {
        lock (_mutationLock)
        {
            ProjectRoot = projectRoot;
            DataDirectory = ProjectLocator.DataDirectoryOf(projectRoot);
            _filesByRelativePath.Clear();
            _recordsById.Clear();
            _duplicateIdRecords.Clear();
            PresentTypes.Clear();

            foreach (var descriptor in ContentTypeRegistry.All)
            {
                var folder = Path.Combine(DataDirectory, descriptor.Folder);
                if (!Directory.Exists(folder))
                    continue;
                PresentTypes.Add(descriptor);

                foreach (var filePath in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                                                  .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    var file = new ContentFileState
                    {
                        AbsolutePath = filePath,
                        RelativePath = ToRelativePath(filePath),
                        TypeId = descriptor.TypeId,
                    };
                    var text = File.ReadAllText(filePath);
                    file.MemoryText = text;
                    file.LastKnownDiskText = text;
                    ParseFile(file);
                    _filesByRelativePath[file.RelativePath] = file;
                }
            }

            RebuildRecordIndex();
            IsLoaded = true;
        }
        Changed?.Invoke();
    }

    private string ToRelativePath(string absolutePath) =>
        Path.GetRelativePath(DataDirectory, absolutePath).Replace('\\', '/');

    /// <summary>Parses a file's memory text and rebuilds its record list. Never throws —
    /// a malformed file keeps its text and carries the error as a problem instead.</summary>
    private static void ParseFile(ContentFileState file)
    {
        file.Records.Clear();
        file.ParseError = null;
        file.Root = null;
        file.Style = JsoncStyle.Detect(file.MemoryText);

        JsoncNode root;
        try
        {
            root = JsoncParser.Parse(file.MemoryText);
        }
        catch (JsoncParseException exception)
        {
            file.ParseError = exception.Message;
            return;
        }
        file.Root = root;

        switch (root)
        {
            case JsoncObject:
                AddRecordFromNode(file, root, arrayIndex: -1);
                break;
            case JsoncArray array:
                for (var index = 0; index < array.Items.Count; index++)
                    AddRecordFromNode(file, array.Items[index], index);
                break;
            default:
                file.ParseError = "The file's root is neither an object nor an array of definitions.";
                break;
        }
    }

    private static void AddRecordFromNode(ContentFileState file, JsoncNode node, int arrayIndex)
    {
        if (node is not JsoncObject objectNode)
            return; // non-object elements surface through validation, not as records
        var value = JsoncPatcher.ToJsonNode(objectNode)!;
        var id = (value["id"] as JsonValue)?.GetValue<string?>() ?? "";
        file.Records.Add(new ContentRecordState
        {
            Id = id,
            TypeId = file.TypeId,
            File = file,
            Node = objectNode,
            ArrayIndex = arrayIndex,
            Value = value,
            Name = (value["name"] as JsonValue)?.GetValue<string?>(),
        });
    }

    private void RebuildRecordIndex()
    {
        _recordsById.Clear();
        _duplicateIdRecords.Clear();
        foreach (var file in _filesByRelativePath.Values)
        {
            foreach (var record in file.Records)
            {
                if (string.IsNullOrWhiteSpace(record.Id))
                    continue;
                if (!_recordsById.TryAdd(record.Id, record))
                    _duplicateIdRecords.Add((record.Id, record));
            }
        }
    }

    // ── Queries ─────────────────────────────────────────────────────────────────────────────

    public IReadOnlyCollection<ContentFileState> AllFiles => _filesByRelativePath.Values;

    public IEnumerable<ContentRecordState> AllRecords => _filesByRelativePath.Values.SelectMany(file => file.Records);

    public IEnumerable<ContentRecordState> RecordsOf(string typeId) =>
        _filesByRelativePath.Values.Where(file => file.TypeId == typeId).SelectMany(file => file.Records);

    public ContentRecordState? FindRecord(string id) => _recordsById.GetValueOrDefault(id);

    public ContentFileState? FindFile(string relativePath) => _filesByRelativePath.GetValueOrDefault(relativePath);

    public IReadOnlyList<(string Id, ContentRecordState Record)> DuplicateIdRecords => _duplicateIdRecords;

    public bool IdExists(string id) => _recordsById.ContainsKey(id);

    /// <summary>The record's current authored text, comments included — the honest raw view.</summary>
    public string RawTextOf(ContentRecordState record) =>
        record.File.MemoryText[record.Node.Start..record.Node.End];

    // ── Mutations ───────────────────────────────────────────────────────────────────────────

    public ContentRecordState EditRecord(string id, JsonNode newValue)
    {
        lock (_mutationLock)
        {
            var record = RequireRecord(id);
            var file = record.File;
            RequireParsed(file);

            var newId = (newValue["id"] as JsonValue)?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(newId))
                throw new InvalidOperationException("A record must keep a non-empty \"id\".");
            if (newId != id && _recordsById.ContainsKey(newId))
                throw new InvalidOperationException($"Cannot rename to '{newId}' — that id already exists.");

            var edits = JsoncPatcher.ComputeValueEdits(file.MemoryText, record.Node, newValue, file.Style);
            if (edits.Count > 0)
            {
                ApplyEditsAndReparse(file, edits);
                MarkRecordDirty(file, newId);
            }
            RebuildRecordIndex();
            var updated = _recordsById.GetValueOrDefault(newId)
                ?? throw new InvalidOperationException("The edit was applied but the record could not be re-read — check the file's raw JSON.");
            NotifyChanged();
            return updated;
        }
    }

    /// <summary>Replaces a record's raw JSONC text (the Advanced editor's apply path).</summary>
    public ContentRecordState EditRecordRawText(string id, string newRawText)
    {
        lock (_mutationLock)
        {
            var record = RequireRecord(id);
            var file = record.File;
            RequireParsed(file);

            JsoncNode parsed;
            try
            {
                parsed = JsoncParser.Parse(newRawText);
            }
            catch (JsoncParseException exception)
            {
                throw new InvalidOperationException($"That text is not valid JSONC: {exception.Message}");
            }
            if (parsed is not JsoncObject)
                throw new InvalidOperationException("A record must be a single JSON object.");

            var newValue = JsoncPatcher.ToJsonNode(parsed) as JsonObject
                ?? throw new InvalidOperationException("A record must be a single JSON object.");
            var newId = (newValue["id"] as JsonValue)?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(newId))
                throw new InvalidOperationException("A record must keep a non-empty \"id\".");
            if (newId != id && _recordsById.ContainsKey(newId))
                throw new InvalidOperationException($"Cannot rename to '{newId}' — that id already exists.");

            ApplyEditsAndReparse(file, new List<TextEdit> { new(record.Node.Start, record.Node.End, newRawText.Trim()) });
            MarkRecordDirty(file, newId);
            RebuildRecordIndex();
            var updated = _recordsById.GetValueOrDefault(newId)
                ?? throw new InvalidOperationException("The raw edit was applied but the record could not be re-read.");
            NotifyChanged();
            return updated;
        }
    }

    public ContentRecordState CreateRecord(string typeId, JsonObject value, string? targetRelativePath, string? insertAfterId = null)
    {
        lock (_mutationLock)
        {
            var descriptor = ContentTypeRegistry.Require(typeId);
            var id = (value["id"] as JsonValue)?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("New records need an \"id\".");
            if (_recordsById.ContainsKey(id))
                throw new InvalidOperationException($"Id '{id}' already exists.");
            if (descriptor.IdPrefix.Length > 0 && !id.StartsWith(descriptor.IdPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"{descriptor.SingularName} ids start with '{descriptor.IdPrefix}'.");

            ContentFileState file;
            if (targetRelativePath is null)
            {
                // No file chosen: single-object convention gets its own file named after the slug.
                var slug = descriptor.IdPrefix.Length > 0 ? id[descriptor.IdPrefix.Length..] : id;
                file = CreateNewFile(descriptor, $"{descriptor.Folder}/{slug}.json", singleObject: true, value);
            }
            else
            {
                var existing = _filesByRelativePath.GetValueOrDefault(targetRelativePath);
                if (existing is null)
                {
                    file = CreateNewFile(descriptor, targetRelativePath, singleObject: false, value);
                }
                else
                {
                    RequireParsed(existing);
                    if (existing.Root is not JsoncArray array)
                        throw new InvalidOperationException($"{targetRelativePath} holds a single record — pick an array file or create a new file.");
                    var anchorIndex = insertAfterId is not null
                        ? existing.Records.FirstOrDefault(candidate => candidate.Id == insertAfterId)?.ArrayIndex ?? -1
                        : -1;
                    var edit = anchorIndex >= 0
                        ? JsoncPatcher.InsertArrayElementAfter(existing.MemoryText, array, anchorIndex, value, existing.Style)
                        : JsoncPatcher.AppendArrayElement(existing.MemoryText, array, value, existing.Style);
                    ApplyEditsAndReparse(existing, new List<TextEdit> { edit });
                    file = existing;
                }
            }

            MarkRecordDirty(file, id);
            RebuildRecordIndex();
            var created = _recordsById.GetValueOrDefault(id)
                ?? throw new InvalidOperationException("The record was written but could not be re-read.");
            NotifyChanged();
            return created;
        }
    }

    public ContentRecordState DuplicateRecord(string sourceId, string newId)
    {
        lock (_mutationLock)
        {
            var source = RequireRecord(sourceId);
            var copy = source.Value.DeepClone().AsObject();
            copy["id"] = newId;
            if (copy["name"] is JsonValue nameValue && nameValue.TryGetValue<string>(out var name))
                copy["name"] = $"{name} (Copy)";
            return CreateRecordUnlocked(source, copy, newId);
        }
    }

    private ContentRecordState CreateRecordUnlocked(ContentRecordState source, JsonObject copy, string newId)
    {
        if (_recordsById.ContainsKey(newId))
            throw new InvalidOperationException($"Id '{newId}' already exists.");

        var file = source.File;
        if (source.ArrayIndex < 0)
        {
            var descriptor = ContentTypeRegistry.Require(source.TypeId);
            var slug = descriptor.IdPrefix.Length > 0 && newId.StartsWith(descriptor.IdPrefix, StringComparison.Ordinal)
                ? newId[descriptor.IdPrefix.Length..]
                : newId.Replace('.', '_');
            file = CreateNewFile(descriptor, $"{descriptor.Folder}/{slug}.json", singleObject: true, copy);
        }
        else
        {
            RequireParsed(file);
            var array = (JsoncArray)file.Root!;
            var edit = JsoncPatcher.InsertArrayElementAfter(file.MemoryText, array, source.ArrayIndex, copy, file.Style);
            ApplyEditsAndReparse(file, new List<TextEdit> { edit });
        }

        MarkRecordDirty(file, newId);
        RebuildRecordIndex();
        var created = _recordsById.GetValueOrDefault(newId)
            ?? throw new InvalidOperationException("The duplicate was written but could not be re-read.");
        NotifyChanged();
        return created;
    }

    public void DeleteRecord(string id)
    {
        lock (_mutationLock)
        {
            var record = RequireRecord(id);
            var file = record.File;
            RequireParsed(file);

            if (record.ArrayIndex < 0)
            {
                // The file IS the record. Empty it in memory; saving deletes the file.
                file.MemoryText = "";
                file.Records.Clear();
                file.Root = null;
                file.ParseError = null;
            }
            else
            {
                var edit = JsoncPatcher.RemoveArrayElement(file.MemoryText, (JsoncArray)file.Root!, record.ArrayIndex);
                ApplyEditsAndReparse(file, new List<TextEdit> { edit });
            }

            RebuildRecordIndex();
            NotifyChanged();
        }
    }

    private ContentFileState CreateNewFile(ContentTypeDescriptor descriptor, string relativePath, bool singleObject, JsonObject firstRecord)
    {
        if (_filesByRelativePath.ContainsKey(relativePath))
            throw new InvalidOperationException($"{relativePath} already exists.");

        var style = new JsoncStyle("  ", Environment.NewLine);
        var body = singleObject
            ? JsoncWriter.WriteMultiLine(firstRecord, style, "") + style.Newline
            : "[" + style.Newline + "  " + JsoncWriter.WriteMultiLine(firstRecord, style, "  ") + style.Newline + "]" + style.Newline;

        var file = new ContentFileState
        {
            AbsolutePath = Path.Combine(DataDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            RelativePath = relativePath,
            TypeId = descriptor.TypeId,
        };
        file.MemoryText = body;
        file.LastKnownDiskText = ""; // never existed on disk → file counts as dirty until saved
        ParseFile(file);
        _filesByRelativePath[relativePath] = file;
        return file;
    }

    private void ApplyEditsAndReparse(ContentFileState file, IReadOnlyList<TextEdit> edits)
    {
        file.MemoryText = JsoncPatcher.ApplyEdits(file.MemoryText, edits);
        ParseFile(file);
        if (file.ParseError is not null)
            throw new InvalidOperationException($"Internal patching error left {file.RelativePath} unparseable: {file.ParseError}");
    }

    private void MarkRecordDirty(ContentFileState file, string recordId)
    {
        foreach (var record in file.Records)
        {
            if (record.Id == recordId)
                record.IsDirty = true;
        }
    }

    // ── Saving, reverting, external changes ─────────────────────────────────────────────────

    public sealed record SaveResult(string RelativePath, bool Saved, string? Error);

    public List<SaveResult> SaveFiles(IEnumerable<string>? relativePaths, BackupService backups, bool overwriteConflicts)
    {
        lock (_mutationLock)
        {
            var targets = (relativePaths is null
                    ? _filesByRelativePath.Values.Where(file => file.IsDirty)
                    : relativePaths.Select(path => _filesByRelativePath.GetValueOrDefault(path)).Where(file => file is not null)!)
                .Cast<ContentFileState>()
                .ToList();

            var results = new List<SaveResult>();
            foreach (var file in targets)
            {
                if (!file.IsDirty)
                {
                    results.Add(new SaveResult(file.RelativePath, false, null));
                    continue;
                }
                if (file.HasDiskConflict && !overwriteConflicts)
                {
                    results.Add(new SaveResult(file.RelativePath, false, "Changed on disk since it was loaded — resolve the conflict first."));
                    continue;
                }

                try
                {
                    if (File.Exists(file.AbsolutePath))
                        backups.BackupCurrentDiskVersion(file.AbsolutePath, ProjectRoot);

                    if (file.MemoryText.Length == 0 && file.Records.Count == 0 && file.Root is null)
                    {
                        // A deleted single-record file.
                        if (File.Exists(file.AbsolutePath))
                            File.Delete(file.AbsolutePath);
                        _filesByRelativePath.Remove(file.RelativePath);
                    }
                    else
                    {
                        AtomicFileWriter.Write(file.AbsolutePath, file.MemoryText);
                        file.LastKnownDiskText = file.MemoryText;
                        foreach (var record in file.Records)
                            record.IsDirty = false;
                    }
                    file.HasDiskConflict = false;
                    results.Add(new SaveResult(file.RelativePath, true, null));
                    FileStateChanged?.Invoke(file, "saved");
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    results.Add(new SaveResult(file.RelativePath, false, exception.Message));
                }
            }
            NotifyChanged();
            return results;
        }
    }

    /// <summary>Swaps a file's in-memory text wholesale (backup restore, conflict resolution).
    /// The change stays unsaved until the user saves, like any other edit.</summary>
    public void ReplaceFileText(string relativePath, string newText)
    {
        lock (_mutationLock)
        {
            var file = _filesByRelativePath.GetValueOrDefault(relativePath)
                ?? throw new KeyNotFoundException($"Unknown file '{relativePath}'.");
            file.MemoryText = newText;
            ParseFile(file);
            foreach (var record in file.Records)
                record.IsDirty = true;
            RebuildRecordIndex();
            NotifyChanged();
        }
    }

    /// <summary>Keeps the in-memory version over an external change. The external disk version
    /// was already recorded as the save baseline, so saving will back it up before overwriting.</summary>
    public void ResolveConflictKeepingMemory(string relativePath)
    {
        lock (_mutationLock)
        {
            var file = _filesByRelativePath.GetValueOrDefault(relativePath)
                ?? throw new KeyNotFoundException($"Unknown file '{relativePath}'.");
            file.HasDiskConflict = false;
        }
        NotifyChanged();
    }

    public void RevertFile(string relativePath)
    {
        lock (_mutationLock)
        {
            var file = _filesByRelativePath.GetValueOrDefault(relativePath)
                ?? throw new KeyNotFoundException($"Unknown file '{relativePath}'.");
            if (!File.Exists(file.AbsolutePath))
            {
                _filesByRelativePath.Remove(relativePath);
            }
            else
            {
                var text = File.ReadAllText(file.AbsolutePath);
                file.MemoryText = text;
                file.LastKnownDiskText = text;
                file.HasDiskConflict = false;
                ParseFile(file);
            }
            RebuildRecordIndex();
            NotifyChanged();
            if (_filesByRelativePath.TryGetValue(relativePath, out var reloaded))
                FileStateChanged?.Invoke(reloaded, "reloaded");
        }
    }

    /// <summary>Called by the file watcher. Clean files reload silently; dirty files flag a conflict.</summary>
    public void OnExternalFileChange(string absolutePath)
    {
        lock (_mutationLock)
        {
            if (!IsLoaded || !absolutePath.StartsWith(DataDirectory, StringComparison.OrdinalIgnoreCase))
                return;
            var relativePath = ToRelativePath(absolutePath);
            var file = _filesByRelativePath.GetValueOrDefault(relativePath);

            if (file is null)
            {
                // A brand-new file appeared (Claude or the user added content outside the tool).
                var descriptor = ContentTypeRegistry.All.FirstOrDefault(candidate =>
                    relativePath.StartsWith(candidate.Folder + "/", StringComparison.OrdinalIgnoreCase));
                if (descriptor is null || !File.Exists(absolutePath))
                    return;
                var created = new ContentFileState
                {
                    AbsolutePath = absolutePath,
                    RelativePath = relativePath,
                    TypeId = descriptor.TypeId,
                };
                var text = TryReadAllText(absolutePath);
                if (text is null)
                    return;
                created.MemoryText = text;
                created.LastKnownDiskText = text;
                ParseFile(created);
                _filesByRelativePath[relativePath] = created;
                RebuildRecordIndex();
                NotifyChanged();
                FileStateChanged?.Invoke(created, "reloaded");
                return;
            }

            if (!File.Exists(absolutePath))
            {
                if (file.IsDirty)
                {
                    file.HasDiskConflict = true;
                    FileStateChanged?.Invoke(file, "conflict");
                    return;
                }
                _filesByRelativePath.Remove(relativePath);
                RebuildRecordIndex();
                NotifyChanged();
                return;
            }

            var diskText = TryReadAllText(absolutePath);
            if (diskText is null || string.Equals(diskText, file.LastKnownDiskText, StringComparison.Ordinal))
                return;

            if (!file.IsDirty)
            {
                file.MemoryText = diskText;
                file.LastKnownDiskText = diskText;
                ParseFile(file);
                RebuildRecordIndex();
                NotifyChanged();
                FileStateChanged?.Invoke(file, "reloaded");
            }
            else
            {
                file.LastKnownDiskText = diskText; // remember what we would be overwriting
                file.HasDiskConflict = true;
                FileStateChanged?.Invoke(file, "conflict");
            }
        }
    }

    private static string? TryReadAllText(string path)
    {
        // Editors and OneDrive hold write locks briefly; retry instead of failing the reload.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                Thread.Sleep(60);
            }
        }
        return null;
    }

    private ContentRecordState RequireRecord(string id) =>
        _recordsById.GetValueOrDefault(id) ?? throw new KeyNotFoundException($"No record with id '{id}'.");

    private static void RequireParsed(ContentFileState file)
    {
        if (file.ParseError is not null)
            throw new InvalidOperationException($"{file.RelativePath} has a syntax error — fix it (or revert the file) before editing records in it: {file.ParseError}");
    }

    private void NotifyChanged() => Changed?.Invoke();
}
