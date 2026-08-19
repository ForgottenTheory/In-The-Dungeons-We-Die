using System.Text.Json;
using System.Text.Json.Nodes;
using ContentStudio.Infrastructure.Jsonc;
using Xunit;

namespace ContentStudio.Tests;

/// <summary>
/// Runs the JSONC engine against every shipped content file. These are the fences that make
/// "the tool edits real game files" trustworthy: the parser must agree with System.Text.Json,
/// and diffing a record against itself must never produce an edit.
/// </summary>
public class RealContentRoundTripTests
{
    private static string DataDirectory
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "game", "data");
                if (Directory.Exists(candidate))
                    return candidate;
            }
            throw new InvalidOperationException("game/data not found above the test directory.");
        }
    }

    private static IEnumerable<string> AllContentFiles() =>
        Directory.EnumerateFiles(DataDirectory, "*.json", SearchOption.AllDirectories);

    [Fact]
    public void EveryShippedFileParses_AndAgreesWithSystemTextJson()
    {
        var referenceOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        foreach (var path in AllContentFiles())
        {
            var text = File.ReadAllText(path);
            var mine = JsoncPatcher.ToJsonNode(JsoncParser.Parse(text));
            var reference = JsonNode.Parse(text, nodeOptions: null, referenceOptions);
            Assert.True(JsonNode.DeepEquals(mine, reference), $"Parser disagrees with System.Text.Json for {path}");
        }
    }

    [Fact]
    public void DiffingEveryShippedRecordAgainstItselfProducesZeroEdits()
    {
        foreach (var path in AllContentFiles())
        {
            var text = File.ReadAllText(path);
            var root = JsoncParser.Parse(text);
            var style = JsoncStyle.Detect(text);

            var recordNodes = root is JsoncArray array ? array.Items : new List<JsoncNode> { root };
            foreach (var node in recordNodes)
            {
                var value = JsoncPatcher.ToJsonNode(node);
                var edits = JsoncPatcher.ComputeValueEdits(text, node, value, style);
                Assert.True(edits.Count == 0, $"Identity diff produced {edits.Count} edit(s) in {path}");
            }
        }
    }

    [Fact]
    public void EditingOneValueInEveryShippedFileLeavesTheRestByteIdentical()
    {
        foreach (var path in AllContentFiles())
        {
            var text = File.ReadAllText(path);
            var root = JsoncParser.Parse(text);
            var style = JsoncStyle.Detect(text);

            var target = (root is JsoncArray array ? array.Items.FirstOrDefault() : root) as JsoncObject;
            var nameMember = target?.FindMember("name");
            if (target is null || nameMember?.Value is not JsoncScalar { Kind: JsonValueKind.String })
                continue;

            var desired = JsoncPatcher.ToJsonNode(target)!.AsObject();
            desired["name"] = "Round Trip Probe";

            var edits = JsoncPatcher.ComputeValueEdits(text, target, desired, style);
            var patched = JsoncPatcher.ApplyEdits(text, edits);

            // Exactly the name scalar changed; everything outside its span is untouched.
            Assert.Single(edits);
            Assert.Equal(text[..edits[0].Start], patched[..edits[0].Start]);
            Assert.Equal(text[edits[0].End..], patched[(edits[0].Start + edits[0].NewText.Length)..]);

            var reparsed = JsoncParser.Parse(patched);
            var probe = (reparsed is JsoncArray reparsedArray ? reparsedArray.Items[0] : reparsed) as JsoncObject;
            var probeName = probe?.FindMember("name")?.Value as JsoncScalar;
            Assert.Equal("\"Round Trip Probe\"", probeName?.RawText);
        }
    }
}
