using System.Text.RegularExpressions;
using ContentStudio.Models;
using ContentStudio.Validation;
using Dungeons.Content;

namespace ContentStudio.Services;

/// <summary>The full validation picture at one moment: every problem, plus per-record and
/// per-type rollups the UI renders as badges.</summary>
public sealed class ValidationState
{
    public required IReadOnlyList<ValidationProblem> Problems { get; init; }
    public required int ErrorCount { get; init; }
    public required int WarningCount { get; init; }
    public required ContentBundle? Bundle { get; init; }

    private ILookup<string, ValidationProblem>? _byRecord;

    public IEnumerable<ValidationProblem> ProblemsOf(string recordId)
    {
        _byRecord ??= Problems.Where(problem => problem.RecordId is not null).ToLookup(problem => problem.RecordId!);
        return _byRecord[recordId];
    }

    public static readonly ValidationState Empty = new()
    {
        Problems = Array.Empty<ValidationProblem>(),
        ErrorCount = 0,
        WarningCount = 0,
        Bundle = null,
    };
}

/// <summary>
/// Runs the complete validation pass: file syntax, per-record game loading, the game's own
/// <see cref="ContentValidator"/> (the same rules that gate startup), and the studio-only
/// checks the game cannot express (unknown fields, id-shaped strings that resolve to nothing).
/// </summary>
public sealed partial class ValidationService
{
    private readonly GameBundleService _bundleService = new();

    [GeneratedRegex("[a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)+")]
    private static partial Regex IdTokens();

    public ValidationState Recompute(ContentWorkspace workspace, ReferenceIndexService referenceIndex)
    {
        var problems = new List<ValidationProblem>();

        foreach (var file in workspace.AllFiles)
        {
            if (file.ParseError is not null)
            {
                problems.Add(new ValidationProblem("error", "load", file.TypeId,
                    $"{file.RelativePath}: {file.ParseError}", null, file.TypeId, file.RelativePath));
            }
            foreach (var record in file.Records.Where(record => record.Id.Length == 0))
            {
                problems.Add(new ValidationProblem("error", "load", file.TypeId,
                    $"{file.RelativePath}: a record is missing its \"id\".", null, file.TypeId, file.RelativePath));
            }
        }

        foreach (var (duplicateId, record) in workspace.DuplicateIdRecords)
        {
            problems.Add(new ValidationProblem("error", "load", record.TypeId,
                $"Duplicate id '{duplicateId}' in {record.File.RelativePath} — the game refuses to load duplicates.",
                duplicateId, record.TypeId, record.File.RelativePath));
        }

        var build = _bundleService.Build(workspace);
        problems.AddRange(build.LoadProblems);

        RunGameValidator(workspace, build.Bundle, problems);
        RunStudioChecks(workspace, referenceIndex, problems);

        return new ValidationState
        {
            Problems = problems,
            ErrorCount = problems.Count(problem => problem.Severity == "error"),
            WarningCount = problems.Count(problem => problem.Severity == "warning"),
            Bundle = build.Bundle,
        };
    }

    private static void RunGameValidator(ContentWorkspace workspace, ContentBundle bundle, List<ValidationProblem> problems)
    {
        IReadOnlyList<ContentProblem> gameProblems;
        try
        {
            gameProblems = ContentValidator.Validate(bundle);
        }
        catch (Exception exception)
        {
            // The validator assumes loadable content; if a half-broken bundle trips it, surface
            // that rather than crashing the tool.
            problems.Add(new ValidationProblem("error", "game-validator", "validator",
                $"The game's ContentValidator itself failed: {exception.Message}", null, null, null));
            return;
        }

        var categoryToType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in ContentTypeRegistry.All)
        {
            foreach (var category in descriptor.ValidatorCategories)
                categoryToType.TryAdd(category, descriptor.TypeId);
        }

        foreach (var gameProblem in gameProblems)
        {
            // The message names the offending definition; the first token that IS a record id wins.
            string? recordId = null;
            foreach (Match match in IdTokens().Matches(gameProblem.Message))
            {
                if (workspace.FindRecord(match.Value) is not null)
                {
                    recordId = match.Value;
                    break;
                }
            }
            var record = recordId is null ? null : workspace.FindRecord(recordId);
            var typeId = record?.TypeId ?? categoryToType.GetValueOrDefault(gameProblem.Category);
            problems.Add(new ValidationProblem("error", "game-validator", gameProblem.Category,
                gameProblem.Message, recordId, typeId, record?.File.RelativePath));
        }
    }

    private static void RunStudioChecks(ContentWorkspace workspace, ReferenceIndexService referenceIndex, List<ValidationProblem> problems)
    {
        foreach (var descriptor in workspace.PresentTypes)
        {
            foreach (var record in workspace.RecordsOf(descriptor.TypeId))
            {
                if (record.Id.Length == 0)
                    continue;
                UnknownFieldScanner.Scan(record, descriptor.DefinitionType, problems);

                if (descriptor.IdPrefix.Length > 0 && !record.Id.StartsWith(descriptor.IdPrefix, StringComparison.Ordinal))
                {
                    problems.Add(new ValidationProblem("warning", "studio", descriptor.TypeId,
                        $"{record.Id}: ids in {descriptor.Folder}/ conventionally start with '{descriptor.IdPrefix}'.",
                        record.Id, descriptor.TypeId, record.File.RelativePath));
                }
            }
        }

        foreach (var (record, fieldPath, unresolvedId) in referenceIndex.UnresolvedIdShapedStrings)
        {
            problems.Add(new ValidationProblem("warning", "studio", record.TypeId,
                $"{record.Id}: \"{fieldPath}\" points at '{unresolvedId}', which does not exist.",
                record.Id, record.TypeId, record.File.RelativePath));
        }
    }
}
