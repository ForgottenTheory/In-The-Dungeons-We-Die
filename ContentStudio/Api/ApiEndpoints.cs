using System.Diagnostics;
using System.Text.Json.Nodes;
using ContentStudio.Analysis;
using ContentStudio.Infrastructure;
using ContentStudio.Models;
using ContentStudio.Services;

namespace ContentStudio.Api;

/// <summary>Every HTTP endpoint Content Studio's UI talks to. Thin handlers only: state
/// changes live in <see cref="ContentWorkspace"/>, analysis in the Analysis classes.</summary>
public static class ApiEndpoints
{
    public static void MapContentStudioApi(this WebApplication app, StudioState state)
    {
        var api = app.MapGroup("/api");

        // ── Project & status ────────────────────────────────────────────────────────────────

        api.MapGet("/status", () => StatusPayload(state));

        api.MapPost("/project", (ProjectRequest request) =>
        {
            try
            {
                state.OpenProject(request.Root);
                return Results.Ok(StatusPayload(state));
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        api.MapGet("/project/browse", (string? path) => BrowseDirectories(path));

        // ── Metadata: types, schemas, vocabularies ──────────────────────────────────────────

        api.MapGet("/meta", () =>
        {
            lock (state.Workspace.MutationLock)
            {
                return Results.Ok(new
                {
                    types = state.Workspace.PresentTypes.Select(descriptor => TypePayload(state, descriptor)).ToList(),
                    vocabulary = VocabularyService.Snapshot(),
                    dynamicVocabulary = DynamicVocabulary(state),
                });
            }
        });

        // ── Records ─────────────────────────────────────────────────────────────────────────

        api.MapGet("/records/{typeId}", (string typeId) =>
        {
            lock (state.Workspace.MutationLock)
            {
                var records = state.Workspace.RecordsOf(typeId)
                    .Select(record => RecordPayload(state, record, includeData: true))
                    .ToList();
                return Results.Ok(new { revision = state.Revision, records });
            }
        });

        api.MapGet("/record/{id}", (string id) =>
        {
            lock (state.Workspace.MutationLock)
            {
                var record = state.Workspace.FindRecord(id);
                return record is null
                    ? Results.NotFound(new { error = $"No record '{id}'." })
                    : Results.Ok(RecordDetailPayload(state, record));
            }
        });

        api.MapPut("/record/{id}", (string id, JsonObject body) =>
            MutateAndRespond(state, () => state.Workspace.EditRecord(id, body)));

        api.MapPut("/record/{id}/raw", (string id, RawEditRequest request) =>
            MutateAndRespond(state, () => state.Workspace.EditRecordRawText(id, request.Text)));

        api.MapPost("/records/{typeId}", (string typeId, CreateRecordRequest request) =>
            MutateAndRespond(state, () => state.Workspace.CreateRecord(typeId, request.Data, request.TargetFile)));

        api.MapPost("/record/{id}/duplicate", (string id, DuplicateRequest request) =>
            MutateAndRespond(state, () => state.Workspace.DuplicateRecord(id, request.NewId)));

        api.MapDelete("/record/{id}", (string id, bool? force) =>
        {
            var incoming = state.References.IncomingOf(id);
            if (incoming.Count > 0 && force is not true)
            {
                lock (state.Workspace.MutationLock)
                {
                    return Results.Conflict(new
                    {
                        error = $"{incoming.Count} other definition(s) reference '{id}'.",
                        referencedBy = incoming.Select(edge => EdgePayload(state, edge, useFrom: true)).ToList(),
                    });
                }
            }
            try
            {
                state.Workspace.DeleteRecord(id);
                var validation = state.RevalidateNow();
                return Results.Ok(new { deleted = id, validation = ValidationSummary(validation) });
            }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        api.MapPost("/bulk", (BulkEditRequest request) => BulkEdit(state, request));

        // ── Validation ──────────────────────────────────────────────────────────────────────

        api.MapGet("/validation", () =>
        {
            var validation = state.CurrentValidation;
            return Results.Ok(new
            {
                revision = state.Revision,
                errors = validation.ErrorCount,
                warnings = validation.WarningCount,
                problems = validation.Problems,
            });
        });

        api.MapPost("/validate", () =>
        {
            var validation = state.RevalidateNow();
            return Results.Ok(new
            {
                revision = state.Revision,
                errors = validation.ErrorCount,
                warnings = validation.WarningCount,
                problems = validation.Problems,
            });
        });

        // ── Files, saving, conflicts, backups ───────────────────────────────────────────────

        api.MapGet("/files", () =>
        {
            lock (state.Workspace.MutationLock)
            {
                return Results.Ok(state.Workspace.AllFiles
                    .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(file => new
                    {
                        path = file.RelativePath,
                        typeId = file.TypeId,
                        dirty = file.IsDirty,
                        conflict = file.HasDiskConflict,
                        parseError = file.ParseError,
                        recordCount = file.Records.Count,
                    }).ToList());
            }
        });

        api.MapPost("/save", (SaveRequest request) =>
        {
            var results = state.Workspace.SaveFiles(request.Files, state.Backups, request.OverwriteConflicts ?? false);
            var validation = state.RevalidateNow();
            return Results.Ok(new { results, validation = ValidationSummary(validation) });
        });

        api.MapPost("/revert", (FileRequest request) =>
        {
            state.Workspace.RevertFile(request.Path);
            var validation = state.RevalidateNow();
            return Results.Ok(new { reverted = request.Path, validation = ValidationSummary(validation) });
        });

        api.MapGet("/file/diff", (string path) =>
        {
            lock (state.Workspace.MutationLock)
            {
                var file = state.Workspace.FindFile(path);
                if (file is null)
                    return Results.NotFound(new { error = $"Unknown file '{path}'." });
                string? diskText = null;
                if (File.Exists(file.AbsolutePath))
                    diskText = File.ReadAllText(file.AbsolutePath);
                return Results.Ok(new { path, memory = file.MemoryText, disk = diskText });
            }
        });

        api.MapPost("/file/keep-mine", (FileRequest request) =>
        {
            state.Workspace.ResolveConflictKeepingMemory(request.Path);
            return Results.Ok(new { resolved = request.Path });
        });

        api.MapGet("/backups", (string path) =>
        {
            var file = state.Workspace.FindFile(path);
            if (file is null)
                return Results.NotFound(new { error = $"Unknown file '{path}'." });
            return Results.Ok(state.Backups.ListVersions(file.AbsolutePath, state.Workspace.ProjectRoot));
        });

        api.MapPost("/backups/restore", (RestoreBackupRequest request) =>
        {
            var file = state.Workspace.FindFile(request.Path);
            if (file is null)
                return Results.NotFound(new { error = $"Unknown file '{request.Path}'." });
            var text = state.Backups.ReadVersion(file.AbsolutePath, state.Workspace.ProjectRoot, request.Version);
            state.Workspace.ReplaceFileText(request.Path, text);
            var validation = state.RevalidateNow();
            return Results.Ok(new { restored = request.Path, version = request.Version, validation = ValidationSummary(validation) });
        });

        api.MapPost("/open", (OpenRequest request) =>
        {
            var file = state.Workspace.FindFile(request.Path);
            if (file is null)
                return Results.NotFound(new { error = $"Unknown file '{request.Path}'." });
            var startInfo = request.Reveal
                ? new ProcessStartInfo("explorer.exe", $"/select,\"{file.AbsolutePath}\"")
                : new ProcessStartInfo(file.AbsolutePath) { UseShellExecute = true };
            Process.Start(startInfo);
            return Results.Ok(new { opened = file.AbsolutePath });
        });

        // ── Dependencies ────────────────────────────────────────────────────────────────────

        api.MapGet("/deps/{id}", (string id) =>
        {
            lock (state.Workspace.MutationLock)
            {
                return Results.Ok(new
                {
                    id,
                    outgoing = state.References.OutgoingOf(id).Select(edge => EdgePayload(state, edge, useFrom: false)).ToList(),
                    incoming = state.References.IncomingOf(id).Select(edge => EdgePayload(state, edge, useFrom: true)).ToList(),
                });
            }
        });

        // ── Analysis / balance ──────────────────────────────────────────────────────────────

        api.MapGet("/analysis/enemies", () => WithBundle(state, bundle => new
        {
            referencePacket = EnemyAnalysis.ReferencePacketAmount,
            rows = EnemyAnalysis.BuildTable(bundle),
        }));

        api.MapGet("/analysis/actor/{id}", (string id) => WithBundle(state, bundle =>
            EnemyAnalysis.ExplainActor(bundle, id) ?? new { error = $"No actor '{id}'." }));

        api.MapGet("/analysis/moves", () => WithBundle(state, bundle => new { rows = MoveAnalysis.BuildTable(bundle) }));

        api.MapGet("/analysis/professions", () => WithBundle(state, bundle =>
        {
            var actions = ProfessionAnalysis.BuildActionTable(bundle);
            return new
            {
                actions,
                professions = ProfessionAnalysis.BuildProfessionSummaries(bundle, actions),
            };
        }));

        api.MapGet("/analysis/loot/table/{id}", (string id, int? depth, bool? active, string? rank) =>
            WithBundle(state, bundle => LootAnalysis.ExpectedValueOf(bundle, id,
                LootContextFrom(bundle, depth, active, rank))));

        api.MapGet("/analysis/loot/item/{id}", (string id, int? depth, bool? active, string? rank) =>
            WithBundle(state, bundle => new
            {
                itemId = id,
                sources = LootAnalysis.SourcesOfItem(bundle, id, LootContextFrom(bundle, depth, active, rank)),
            }));

        api.MapGet("/analysis/loot/overview", () => WithBundle(state, LootAnalysis.BuildOverview));

        api.MapGet("/analysis/warnings", () => WithBundle(state, bundle => BalanceWarningAggregator.Collect(bundle)));

        api.MapGet("/analysis/realm/{id}", (string id) => WithBundle(state, bundle =>
        {
            if (!bundle.Realms.TryGetById(id, out var realm))
                return (object)new { error = $"No realm '{id}'." };
            return new
            {
                id = realm.Id,
                name = realm.Name,
                tags = realm.Tags,
                supportedTiers = realm.SupportedTiers,
                locations = realm.Locations.Select(location => new
                {
                    id = location.Id,
                    name = location.Name,
                    type = location.Type.ToString(),
                    depth = location.Depth,
                    connections = location.Connections,
                    actorId = location.ActorId,
                    professionActionId = location.ProfessionActionId,
                    lootTableId = location.LootTableId,
                    hidden = location.Hidden,
                }).ToList(),
            };
        }));

        // ── Events ──────────────────────────────────────────────────────────────────────────

        api.MapGet("/events", (HttpContext context) => state.Sse.HandleConnection(context));
    }

    // ── Request DTOs ────────────────────────────────────────────────────────────────────────

    public sealed record ProjectRequest(string Root);
    public sealed record RawEditRequest(string Text);
    public sealed record CreateRecordRequest(JsonObject Data, string? TargetFile);
    public sealed record DuplicateRequest(string NewId);
    public sealed record SaveRequest(List<string>? Files, bool? OverwriteConflicts);
    public sealed record FileRequest(string Path);
    public sealed record RestoreBackupRequest(string Path, string Version);
    public sealed record OpenRequest(string Path, bool Reveal);
    public sealed record BulkEditRequest(List<string> Ids, SetFieldOperation? Set, string? AddTag, string? RemoveTag);
    public sealed record SetFieldOperation(string Path, JsonNode? Value);

    // ── Payload builders ────────────────────────────────────────────────────────────────────

    private static object StatusPayload(StudioState state)
    {
        var validation = state.CurrentValidation;
        lock (state.Workspace.MutationLock)
        {
            return new
            {
                projectRoot = state.Workspace.IsLoaded ? state.Workspace.ProjectRoot : null,
                loaded = state.Workspace.IsLoaded,
                revision = state.Revision,
                recordCount = state.Workspace.IsLoaded ? state.Workspace.AllRecords.Count() : 0,
                dirtyFiles = state.Workspace.IsLoaded
                    ? state.Workspace.AllFiles.Where(file => file.IsDirty).Select(file => file.RelativePath).ToList()
                    : new List<string>(),
                conflictFiles = state.Workspace.IsLoaded
                    ? state.Workspace.AllFiles.Where(file => file.HasDiskConflict).Select(file => file.RelativePath).ToList()
                    : new List<string>(),
                errors = validation.ErrorCount,
                warnings = validation.WarningCount,
            };
        }
    }

    private static object TypePayload(StudioState state, ContentTypeDescriptor descriptor)
    {
        var files = state.Workspace.AllFiles
            .Where(file => file.TypeId == descriptor.TypeId)
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new
        {
            typeId = descriptor.TypeId,
            displayName = descriptor.DisplayName,
            singularName = descriptor.SingularName,
            group = descriptor.NavigationGroup,
            idPrefix = descriptor.IdPrefix,
            description = descriptor.Description,
            listColumns = descriptor.ListColumns,
            recordCount = files.Sum(file => file.Records.Count),
            files = files.Select(file => new
            {
                path = file.RelativePath,
                recordCount = file.Records.Count,
                isArrayFile = file.Records.Count != 1 || file.Records.FirstOrDefault()?.ArrayIndex >= 0,
            }).ToList(),
            schema = SchemaGenerator.GenerateFor(descriptor.DefinitionType),
        };
    }

    private static object RecordPayload(StudioState state, ContentRecordState record, bool includeData)
    {
        var problems = state.CurrentValidation.ProblemsOf(record.Id).ToList();
        return new
        {
            id = record.Id,
            typeId = record.TypeId,
            name = record.Name,
            file = record.File.RelativePath,
            dirty = record.IsDirty,
            fileDirty = record.File.IsDirty,
            conflict = record.File.HasDiskConflict,
            errors = problems.Count(problem => problem.Severity == "error"),
            warnings = problems.Count(problem => problem.Severity == "warning"),
            data = includeData ? record.Value.DeepClone() : null,
        };
    }

    private static object RecordDetailPayload(StudioState state, ContentRecordState record) => new
    {
        record = RecordPayload(state, record, includeData: true),
        rawText = state.Workspace.RawTextOf(record),
        problems = state.CurrentValidation.ProblemsOf(record.Id).ToList(),
        outgoing = state.References.OutgoingOf(record.Id).Select(edge => EdgePayload(state, edge, useFrom: false)).ToList(),
        incoming = state.References.IncomingOf(record.Id).Select(edge => EdgePayload(state, edge, useFrom: true)).ToList(),
    };

    private static object EdgePayload(StudioState state, ReferenceEdge edge, bool useFrom)
    {
        var otherId = useFrom ? edge.FromId : edge.ToId;
        var other = state.Workspace.FindRecord(otherId);
        return new
        {
            id = otherId,
            name = other?.Name,
            typeId = other?.TypeId,
            fieldPath = edge.FieldPath,
        };
    }

    private static object ValidationSummary(ValidationState validation) => new
    {
        errors = validation.ErrorCount,
        warnings = validation.WarningCount,
    };

    private static IResult MutateAndRespond(StudioState state, Func<ContentRecordState> mutate)
    {
        try
        {
            var record = mutate();
            var validation = state.RevalidateNow();
            lock (state.Workspace.MutationLock)
            {
                return Results.Ok(new
                {
                    record = RecordPayload(state, record, includeData: true),
                    rawText = state.Workspace.RawTextOf(record),
                    problems = validation.ProblemsOf(record.Id).ToList(),
                    validation = ValidationSummary(validation),
                });
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static IResult BulkEdit(StudioState state, BulkEditRequest request)
    {
        var applied = new List<string>();
        var failed = new List<object>();
        foreach (var id in request.Ids)
        {
            try
            {
                var record = state.Workspace.FindRecord(id)
                    ?? throw new KeyNotFoundException($"No record '{id}'.");
                var updated = record.Value.DeepClone().AsObject();

                if (request.Set is not null)
                    SetValueAtPath(updated, request.Set.Path, request.Set.Value?.DeepClone());
                if (request.AddTag is { Length: > 0 } addTag)
                {
                    var currentTags = (updated["tags"] as JsonArray)?.Select(tag => (tag as JsonValue)?.GetValue<string?>())
                        .Where(tag => tag is not null).Cast<string>().ToList() ?? new List<string>();
                    if (!currentTags.Contains(addTag))
                        currentTags.Add(addTag);
                    updated["tags"] = new JsonArray(currentTags.Select(tag => (JsonNode)JsonValue.Create(tag)!).ToArray());
                }
                if (request.RemoveTag is { Length: > 0 } removeTag && updated["tags"] is JsonArray existingTags)
                {
                    for (var index = existingTags.Count - 1; index >= 0; index--)
                    {
                        if ((existingTags[index] as JsonValue)?.GetValue<string?>() == removeTag)
                            existingTags.RemoveAt(index);
                    }
                }

                state.Workspace.EditRecord(id, updated);
                applied.Add(id);
            }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
            {
                failed.Add(new { id, error = exception.Message });
            }
        }
        var validation = state.RevalidateNow();
        return Results.Ok(new { applied, failed, validation = ValidationSummary(validation) });
    }

    /// <summary>Sets a value at a dot path ("armor.resistances.heat"), creating objects on the way.</summary>
    private static void SetValueAtPath(JsonObject root, string path, JsonNode? value)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            throw new InvalidOperationException("Empty field path.");
        var current = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (current[segments[index]] is not JsonObject next)
            {
                next = new JsonObject();
                current[segments[index]] = next;
            }
            current = next;
        }
        if (value is null)
            current.Remove(segments[^1]);
        else
            current[segments[^1]] = value;
    }

    private static IResult WithBundle(StudioState state, Func<Dungeons.Content.ContentBundle, object> analyze)
    {
        var bundle = state.CurrentValidation.Bundle;
        if (bundle is null)
            return Results.Ok(new { error = "No project loaded yet." });
        return Results.Ok(analyze(bundle));
    }

    private static LootAnalysis.LootEvaluationContext LootContextFrom(
        Dungeons.Content.ContentBundle bundle, int? depth, bool? active, string? rank)
    {
        var realmTags = bundle.Realms.GetAll().SelectMany(realm => realm.Tags.Concat(new[] { realm.Id }));
        return LootAnalysis.LootEvaluationContext.From(depth ?? 1, active ?? true, rank, (depth ?? 1) > 0 ? realmTags : null);
    }

    /// <summary>Vocabularies that live in the project's data rather than in Core statics —
    /// recomputed per request so they always reflect current (unsaved) edits.</summary>
    private static object DynamicVocabulary(StudioState state)
    {
        var workspace = state.Workspace;

        var essenceKeys = workspace.RecordsOf("essences")
            .Select(record => record.Id.StartsWith("essence.", StringComparison.Ordinal) ? record.Id["essence.".Length..] : record.Id)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var propertyNames = workspace.RecordsOf("properties")
            .Select(record => record.Id)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var moveTagsAll = Dungeons.Combat.MoveTags.Actions
            .Concat(Dungeons.Combat.MoveTags.Deliveries)
            .Concat(Dungeons.Combat.MoveTags.Forms)
            .Concat(Dungeons.Combat.MoveTags.Mechs)
            .Concat(Dungeons.Combat.MoveTags.Essences)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var affixFamilies = workspace.RecordsOf("affixes")
            .Select(record => (record.Value["family"] as JsonValue)?.GetValue<string?>())
            .Where(family => !string.IsNullOrEmpty(family))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(family => family, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lootContextTags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "active", "passive", "in_realm", "elite", "boss" };
        foreach (var realm in workspace.RecordsOf("realms"))
        {
            lootContextTags.Add(realm.Id);
            foreach (var tag in TagListOf(realm.Value))
                lootContextTags.Add(tag);
        }
        foreach (var table in workspace.RecordsOf("loot_tables"))
        {
            foreach (var tag in TagListOf(table.Value))
                lootContextTags.Add(tag);
        }

        var materialTags = workspace.RecordsOf("materials")
            .SelectMany(record => TagListOf(record.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new
        {
            essenceKeys,
            propertyNames,
            moveTagsAll,
            moveOpFields = Dungeons.Combat.MoveOps.TimingFields.Concat(Dungeons.Combat.MoveOps.Flags)
                .OrderBy(field => field, StringComparer.OrdinalIgnoreCase).ToList(),
            damageLanesAndPhysical = Dungeons.Combat.DamageLanes.All.OrderBy(lane => lane).ToList(),
            affixFamilies,
            lootContextTags = lootContextTags.ToList(),
            materialTags,
        };
    }

    private static IEnumerable<string> TagListOf(JsonNode value) =>
        (value["tags"] as JsonArray)?.Select(tag => (tag as JsonValue)?.GetValue<string?>())
            .Where(tag => !string.IsNullOrEmpty(tag)).Cast<string>() ?? Enumerable.Empty<string>();

    private static IResult BrowseDirectories(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.Ok(new
                {
                    path = "",
                    parent = (string?)null,
                    directories = DriveInfo.GetDrives()
                        .Where(drive => drive.IsReady)
                        .Select(drive => new { name = drive.Name, path = drive.Name, isProject = false })
                        .ToList(),
                });
            }
            var info = new DirectoryInfo(path);
            if (!info.Exists)
                return Results.BadRequest(new { error = $"Directory not found: {path}" });
            return Results.Ok(new
            {
                path = info.FullName,
                parent = info.Parent?.FullName ?? "",
                isProject = ProjectLocator.IsValidProjectRoot(info.FullName),
                directories = info.EnumerateDirectories()
                    .Where(directory => (directory.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(directory => new
                    {
                        name = directory.Name,
                        path = directory.FullName,
                        isProject = ProjectLocator.IsValidProjectRoot(directory.FullName),
                    })
                    .ToList(),
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}
