using System.Text.Json;

namespace ContentStudio.Infrastructure.Jsonc;

/// <summary>
/// A parsed JSONC value that remembers exactly where it lives in the source text.
/// Spans are half-open character ranges [<see cref="Start"/>, <see cref="End"/>) over the
/// original document, which is what lets <see cref="JsoncPatcher"/> edit a file surgically
/// while leaving every comment and every untouched line byte-for-byte intact.
/// </summary>
public abstract class JsoncNode
{
    /// <summary>Index of the value's first character (the opening brace/bracket/quote/digit).</summary>
    public int Start { get; init; }

    /// <summary>Index one past the value's last character.</summary>
    public int End { get; internal set; }
}

public sealed class JsoncObject : JsoncNode
{
    public List<JsoncMember> Members { get; } = new();

    public JsoncMember? FindMember(string name) => Members.FirstOrDefault(member => member.Name == name);
}

/// <summary>One <c>"name": value</c> pair inside a <see cref="JsoncObject"/>.</summary>
public sealed class JsoncMember
{
    public required string Name { get; init; }

    /// <summary>Index of the opening quote of the member name.</summary>
    public required int NameStart { get; init; }

    public required JsoncNode Value { get; init; }
}

public sealed class JsoncArray : JsoncNode
{
    public List<JsoncNode> Items { get; } = new();
}

/// <summary>A string, number, boolean or null, kept as raw source text.</summary>
public sealed class JsoncScalar : JsoncNode
{
    public required JsonValueKind Kind { get; init; }

    /// <summary>The exact source slice, e.g. <c>"Iron Ore"</c> or <c>0.25</c>.</summary>
    public required string RawText { get; init; }
}

public sealed class JsoncParseException : Exception
{
    public int Position { get; }
    public int Line { get; }
    public int Column { get; }

    public JsoncParseException(string message, int position, int line, int column)
        : base($"{message} (line {line}, column {column})")
    {
        Position = position;
        Line = line;
        Column = column;
    }
}
