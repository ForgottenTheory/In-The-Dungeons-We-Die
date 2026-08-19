using System.Text.Json.Nodes;
using ContentStudio.Infrastructure.Jsonc;

namespace ContentStudio.Models;

/// <summary>One content file held in memory: its current (possibly edited) text plus the
/// span-tracking parse that record edits are patched through.</summary>
public sealed class ContentFileState
{
    public required string AbsolutePath { get; init; }

    /// <summary>Path relative to <c>game/data</c>, forward slashes — the id the UI uses.</summary>
    public required string RelativePath { get; init; }

    public required string TypeId { get; init; }

    /// <summary>The current text, including unsaved edits. Saving writes exactly this.</summary>
    public string MemoryText { get; set; } = "";

    /// <summary>The disk text as of the last load or save — the dirty baseline.</summary>
    public string LastKnownDiskText { get; set; } = "";

    public JsoncStyle Style { get; set; } = new("  ", "\n");
    public JsoncNode? Root { get; set; }
    public string? ParseError { get; set; }

    /// <summary>True when the file changed on disk while unsaved edits exist here.</summary>
    public bool HasDiskConflict { get; set; }

    public bool IsDirty => !string.Equals(MemoryText, LastKnownDiskText, StringComparison.Ordinal);

    public List<ContentRecordState> Records { get; } = new();
}

/// <summary>One authored record (one definition) and where it lives.</summary>
public sealed class ContentRecordState
{
    public required string Id { get; set; }
    public required string TypeId { get; init; }
    public required ContentFileState File { get; init; }

    /// <summary>The record's node inside its file's parse tree; refreshed on every reparse.</summary>
    public required JsoncNode Node { get; set; }

    /// <summary>Index in the file's root array, or -1 when the file IS the record.</summary>
    public required int ArrayIndex { get; set; }

    /// <summary>The record as a plain JSON value — what the API serves and editors mutate.</summary>
    public required JsonNode Value { get; set; }

    public string? Name { get; set; }

    /// <summary>Records touched since the file was last saved (drives the modified badge).</summary>
    public bool IsDirty { get; set; }
}

/// <summary>A single validation finding, mapped as precisely as the message allows.</summary>
public sealed record ValidationProblem(
    string Severity,          // "error" | "warning"
    string Source,            // "load" | "game-validator" | "studio"
    string Category,
    string Message,
    string? RecordId,
    string? TypeId,
    string? FilePath);
