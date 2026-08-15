namespace Dungeons.Content;

/// <summary>
/// Thrown when loaded content fails cross-reference validation. Carries the full list
/// of problems so the composition root can fail loudly at startup instead of letting
/// a bad reference surface later as a <see cref="KeyNotFoundException"/> deep in play
/// (docs/json-schema.md §21).
/// </summary>
public sealed class ContentValidationException : Exception
{
    public IReadOnlyList<ContentProblem> Problems { get; }

    public ContentValidationException(IReadOnlyList<ContentProblem> problems)
        : base(BuildMessage(problems))
    {
        Problems = problems;
    }

    private static string BuildMessage(IReadOnlyList<ContentProblem> problems)
    {
        var header = $"Content validation found {problems.Count} problem(s):";
        return string.Join(Environment.NewLine, problems.Select(p => "  " + p).Prepend(header));
    }
}
