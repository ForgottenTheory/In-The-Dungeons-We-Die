using System.Text;
using System.Text.Json;

namespace ContentStudio.Infrastructure.Jsonc;

/// <summary>
/// A JSONC parser that produces a span-tracking tree (<see cref="JsoncNode"/>) instead of a
/// plain value model. It accepts exactly what the game's <c>DataStore&lt;T&gt;</c> accepts —
/// <c>//</c> and <c>/* */</c> comments, trailing commas, an optional BOM — because the tool
/// must be able to open every file the game can load.
/// </summary>
public static class JsoncParser
{
    public static JsoncNode Parse(string text)
    {
        var position = 0;
        SkipTrivia(text, ref position);
        var root = ParseValue(text, ref position);
        SkipTrivia(text, ref position);
        if (position < text.Length)
            throw ErrorAt(text, position, "Unexpected content after the end of the JSON value");
        return root;
    }

    private static JsoncNode ParseValue(string text, ref int position)
    {
        if (position >= text.Length)
            throw ErrorAt(text, position, "Unexpected end of file while expecting a value");

        return text[position] switch
        {
            '{' => ParseObject(text, ref position),
            '[' => ParseArray(text, ref position),
            '"' => ParseString(text, ref position),
            't' or 'f' or 'n' => ParseKeywordLiteral(text, ref position),
            '-' or (>= '0' and <= '9') => ParseNumber(text, ref position),
            _ => throw ErrorAt(text, position, $"Unexpected character '{text[position]}'"),
        };
    }

    private static JsoncObject ParseObject(string text, ref int position)
    {
        var objectNode = new JsoncObject { Start = position };
        position++; // consume '{'

        while (true)
        {
            SkipTrivia(text, ref position);
            if (position >= text.Length)
                throw ErrorAt(text, position, "Unterminated object");
            if (text[position] == '}')
            {
                position++;
                objectNode.End = position;
                return objectNode;
            }

            var nameStart = position;
            var nameScalar = ParseString(text, ref position);
            var memberName = DecodeStringScalar(nameScalar);

            SkipTrivia(text, ref position);
            if (position >= text.Length || text[position] != ':')
                throw ErrorAt(text, position, $"Expected ':' after member name \"{memberName}\"");
            position++; // consume ':'
            SkipTrivia(text, ref position);

            var value = ParseValue(text, ref position);
            objectNode.Members.Add(new JsoncMember { Name = memberName, NameStart = nameStart, Value = value });

            SkipTrivia(text, ref position);
            if (position < text.Length && text[position] == ',')
            {
                position++; // consume ',' — a trailing comma before '}' is tolerated on the next loop
                continue;
            }
            if (position < text.Length && text[position] == '}')
            {
                position++;
                objectNode.End = position;
                return objectNode;
            }
            throw ErrorAt(text, position, "Expected ',' or '}' in object");
        }
    }

    private static JsoncArray ParseArray(string text, ref int position)
    {
        var arrayNode = new JsoncArray { Start = position };
        position++; // consume '['

        while (true)
        {
            SkipTrivia(text, ref position);
            if (position >= text.Length)
                throw ErrorAt(text, position, "Unterminated array");
            if (text[position] == ']')
            {
                position++;
                arrayNode.End = position;
                return arrayNode;
            }

            arrayNode.Items.Add(ParseValue(text, ref position));

            SkipTrivia(text, ref position);
            if (position < text.Length && text[position] == ',')
            {
                position++;
                continue;
            }
            if (position < text.Length && text[position] == ']')
            {
                position++;
                arrayNode.End = position;
                return arrayNode;
            }
            throw ErrorAt(text, position, "Expected ',' or ']' in array");
        }
    }

    private static JsoncScalar ParseString(string text, ref int position)
    {
        if (text[position] != '"')
            throw ErrorAt(text, position, "Expected a string");

        var start = position;
        position++; // consume opening quote
        while (position < text.Length)
        {
            var character = text[position];
            if (character == '\\')
            {
                position += 2; // skip the escape pair; \uXXXX digits are plain chars from here
                continue;
            }
            if (character == '"')
            {
                position++;
                return new JsoncScalar
                {
                    Start = start,
                    End = position,
                    Kind = JsonValueKind.String,
                    RawText = text[start..position],
                };
            }
            position++;
        }
        throw ErrorAt(text, start, "Unterminated string");
    }

    private static JsoncScalar ParseNumber(string text, ref int position)
    {
        var start = position;
        if (text[position] == '-')
            position++;
        while (position < text.Length && IsNumberCharacter(text[position]))
            position++;

        var raw = text[start..position];
        if (!double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
            throw ErrorAt(text, start, $"Invalid number '{raw}'");

        return new JsoncScalar { Start = start, End = position, Kind = JsonValueKind.Number, RawText = raw };
    }

    private static bool IsNumberCharacter(char character) =>
        character is (>= '0' and <= '9') or '.' or 'e' or 'E' or '+' or '-';

    private static JsoncScalar ParseKeywordLiteral(string text, ref int position)
    {
        foreach (var (keyword, kind) in KeywordLiterals)
        {
            if (position + keyword.Length <= text.Length &&
                text.AsSpan(position, keyword.Length).SequenceEqual(keyword))
            {
                var scalar = new JsoncScalar
                {
                    Start = position,
                    End = position + keyword.Length,
                    Kind = kind,
                    RawText = keyword,
                };
                position += keyword.Length;
                return scalar;
            }
        }
        throw ErrorAt(text, position, "Expected 'true', 'false' or 'null'");
    }

    private static readonly (string Keyword, JsonValueKind Kind)[] KeywordLiterals =
    {
        ("true", JsonValueKind.True),
        ("false", JsonValueKind.False),
        ("null", JsonValueKind.Null),
    };

    /// <summary>Skips whitespace, a BOM, <c>//</c> line comments and <c>/* */</c> block comments.</summary>
    public static void SkipTrivia(string text, ref int position)
    {
        while (position < text.Length)
        {
            var character = text[position];
            if (character is ' ' or '\t' or '\r' or '\n' or '﻿')
            {
                position++;
                continue;
            }
            if (character == '/' && position + 1 < text.Length)
            {
                if (text[position + 1] == '/')
                {
                    position += 2;
                    while (position < text.Length && text[position] != '\n')
                        position++;
                    continue;
                }
                if (text[position + 1] == '*')
                {
                    var closingIndex = text.IndexOf("*/", position + 2, StringComparison.Ordinal);
                    if (closingIndex < 0)
                        throw ErrorAt(text, position, "Unterminated block comment");
                    position = closingIndex + 2;
                    continue;
                }
            }
            return;
        }
    }

    /// <summary>Decodes a string scalar's raw source text (quotes and escapes) into its value.</summary>
    public static string DecodeStringScalar(JsoncScalar scalar)
    {
        var raw = scalar.RawText;
        if (!raw.Contains('\\'))
            return raw[1..^1];

        var builder = new StringBuilder(raw.Length);
        for (var index = 1; index < raw.Length - 1; index++)
        {
            var character = raw[index];
            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }
            index++;
            switch (raw[index])
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    builder.Append((char)Convert.ToInt32(raw.Substring(index + 1, 4), 16));
                    index += 4;
                    break;
                default: builder.Append(raw[index]); break;
            }
        }
        return builder.ToString();
    }

    private static JsoncParseException ErrorAt(string text, int position, string message)
    {
        var line = 1;
        var lineStart = 0;
        for (var index = 0; index < position && index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }
        return new JsoncParseException(message, position, line, position - lineStart + 1);
    }
}
