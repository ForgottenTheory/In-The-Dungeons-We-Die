using System.Text.Json.Nodes;
using ContentStudio.Infrastructure.Jsonc;
using Xunit;

namespace ContentStudio.Tests;

public class JsoncPatcherTests
{
    private static string Patch(string source, Func<JsoncNode, JsonNode> makeDesired)
    {
        var root = JsoncParser.Parse(source);
        var desired = makeDesired(root);
        var edits = JsoncPatcher.ComputeValueEdits(source, root, desired, JsoncStyle.Detect(source));
        var result = JsoncPatcher.ApplyEdits(source, edits);
        JsoncParser.Parse(result); // every patch must leave valid JSONC behind
        return result;
    }

    private static JsonNode ValueOf(JsoncNode node) => JsoncPatcher.ToJsonNode(node)!;

    [Fact]
    public void ChangingOneNumberTouchesNothingElse()
    {
        var source = """
            {
              // The brute hits hard.
              "id": "actor.brute",
              "health": 180, // baseline before role
              "armor": 45
            }
            """;
        var desired = ValueOf(JsoncParser.Parse(source));
        desired["health"] = 200;

        var result = Patch(source, _ => desired);

        Assert.Contains("\"health\": 200, // baseline before role", result);
        Assert.Contains("// The brute hits hard.", result);
        Assert.Contains("\"armor\": 45", result);
    }

    [Fact]
    public void EquivalentNumberFormattingIsNotAnEdit()
    {
        var source = """{ "chance": 0.50, "picks": 1 }""";
        var root = JsoncParser.Parse(source);
        var desired = ValueOf(root);
        desired["chance"] = 0.5;

        var edits = JsoncPatcher.ComputeValueEdits(source, root, desired, JsoncStyle.Detect(source));
        Assert.Empty(edits);
    }

    [Fact]
    public void AddingAndRemovingMembersKeepsNeighbours()
    {
        var source = """
            {
              "id": "move.slash",
              "stagger_power": 10,
              "description": "A cut."
            }
            """;
        var desired = ValueOf(JsoncParser.Parse(source)).AsObject();
        desired.Remove("stagger_power");
        desired["cooldown_ticks"] = 40;

        var result = Patch(source, _ => desired);

        Assert.DoesNotContain("stagger_power", result);
        Assert.Contains("\"cooldown_ticks\": 40", result);
        Assert.Contains("\"description\": \"A cut.\"", result);
        var roundTripped = ValueOf(JsoncParser.Parse(result));
        Assert.True(JsonNode.DeepEquals(roundTripped, desired));
    }

    [Fact]
    public void SingleLineRecordsStaySingleLine()
    {
        var source = """
            [
              { "id": "material.granite", "name": "Granite", "properties": { "hardness": 68 } },
              { "id": "material.slate", "name": "Slate", "properties": { "hardness": 50 } }
            ]
            """;
        var root = (JsoncArray)JsoncParser.Parse(source);
        var desired = (JsonArray)ValueOf(root);
        desired[0]!["properties"]!["hardness"] = 70;

        var result = Patch(source, _ => desired);

        Assert.Contains("{ \"id\": \"material.granite\", \"name\": \"Granite\", \"properties\": { \"hardness\": 70 } }", result);
    }

    [Fact]
    public void AppendedRecordMatchesTheFilesElementStyle()
    {
        var source = """
            [
              { "id": "material.granite", "name": "Granite" }
            ]
            """;
        var root = (JsoncArray)JsoncParser.Parse(source);
        var element = new JsonObject { ["id"] = "material.basalt", ["name"] = "Basalt" };
        var edit = JsoncPatcher.AppendArrayElement(source, root, element, JsoncStyle.Detect(source));
        var result = JsoncPatcher.ApplyEdits(source, new[] { edit });

        Assert.Contains("{ \"id\": \"material.basalt\", \"name\": \"Basalt\" }", result);
        var parsed = (JsoncArray)JsoncParser.Parse(result);
        Assert.Equal(2, parsed.Items.Count);
    }

    [Fact]
    public void RemovingAnElementTakesItsAttachedCommentButNotSectionHeaders()
    {
        var source = """
            [
              // ══ SECTION: STONES ══

              // Granite is the baseline stone.
              { "id": "material.granite" },
              { "id": "material.slate" }
            ]
            """;
        var root = (JsoncArray)JsoncParser.Parse(source);
        var edit = JsoncPatcher.RemoveArrayElement(source, root, 0);
        var result = JsoncPatcher.ApplyEdits(source, new[] { edit });

        Assert.DoesNotContain("Granite is the baseline stone", result);
        Assert.Contains("SECTION: STONES", result);
        Assert.Contains("material.slate", result);
        var parsed = (JsoncArray)JsoncParser.Parse(result);
        Assert.Single(parsed.Items);
    }

    [Fact]
    public void RemovingTheLastElementRemovesThePrecedingComma()
    {
        var source = """
            [
              { "id": "a" },
              { "id": "b" }
            ]
            """;
        var root = (JsoncArray)JsoncParser.Parse(source);
        var edit = JsoncPatcher.RemoveArrayElement(source, root, 1);
        var result = JsoncPatcher.ApplyEdits(source, new[] { edit });
        var parsed = (JsoncArray)JsoncParser.Parse(result);
        Assert.Single(parsed.Items);
        Assert.DoesNotContain("\"b\"", result);
    }

    [Fact]
    public void ReorderingObjectsWithIdsCarriesTheirCommentsAlong()
    {
        var source = """
            [
              {
                // fast but weak
                "id": "move.jab", "amount": 3
              },
              {
                // slow but heavy
                "id": "move.smash", "amount": 20
              }
            ]
            """;
        var root = (JsoncArray)JsoncParser.Parse(source);
        var desired = (JsonArray)ValueOf(root);
        var first = desired[0]!.DeepClone();
        var second = desired[1]!.DeepClone();
        desired.RemoveAt(1);
        desired.RemoveAt(0);
        desired.Add(second);
        desired.Add(first);

        var result = Patch(source, _ => desired);

        Assert.True(result.IndexOf("move.smash", StringComparison.Ordinal) < result.IndexOf("move.jab", StringComparison.Ordinal));
        Assert.Contains("// slow but heavy", result);
        Assert.Contains("// fast but weak", result);
    }

    [Fact]
    public void InsertAfterPlacesTheDuplicateNextToItsSource()
    {
        var source = """
            [
              { "id": "a" },
              { "id": "b" }
            ]
            """;
        var root = (JsoncArray)JsoncParser.Parse(source);
        var copy = new JsonObject { ["id"] = "a_copy" };
        var edit = JsoncPatcher.InsertArrayElementAfter(source, root, 0, copy, JsoncStyle.Detect(source));
        var result = JsoncPatcher.ApplyEdits(source, new[] { edit });

        var parsed = (JsoncArray)JsoncParser.Parse(result);
        Assert.Equal(3, parsed.Items.Count);
        var ids = parsed.Items.Select(item => ((JsoncScalar)((JsoncObject)item).FindMember("id")!.Value).RawText).ToList();
        Assert.Equal(new[] { "\"a\"", "\"a_copy\"", "\"b\"" }, ids);
    }

    [Fact]
    public void TrailingCommasAreToleratedAndNeverEmitted()
    {
        var source = """
            {
              "tags": ["a", "b",],
            }
            """;
        var desired = ValueOf(JsoncParser.Parse(source)).AsObject();
        ((JsonArray)desired["tags"]!).Add("c");

        var result = Patch(source, _ => desired);
        var parsed = ValueOf(JsoncParser.Parse(result));
        Assert.True(JsonNode.DeepEquals(parsed, desired));
    }

    [Fact]
    public void CrlfFilesKeepCrlfOnInsert()
    {
        var source = "{\r\n  \"id\": \"x\",\r\n  \"name\": \"X\"\r\n}\r\n".TrimEnd();
        var desired = ValueOf(JsoncParser.Parse(source)).AsObject();
        desired["extra"] = 1;

        var result = Patch(source, _ => desired);
        Assert.Contains("\r\n  \"extra\": 1", result);
    }
}
