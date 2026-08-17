using Dungeons.Crafting;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The failure vocabulary — the one piece of craft wording that stayed in
/// <see cref="CraftFormat"/> after D30. The player-facing pre-commit voice moved to
/// <c>SemanticFormat</c> (tests/Presentation/SemanticFormatTests.cs, where the §6.2c
/// guarantees are now pinned); the numeric voice moved to <c>AdvancedFormat</c>.
/// </summary>
public class CraftFormatTests
{
    [Fact]
    public void EveryFailureHasAPlayerFacingMessage()
    {
        foreach (var failure in Enum.GetValues<CraftFailure>())
        {
            var message = CraftFormat.Failure(failure);

            if (failure == CraftFailure.None)
                Assert.Equal(string.Empty, message);
            else
                Assert.False(string.IsNullOrWhiteSpace(message), $"{failure} has no message.");
        }
    }

    [Fact]
    public void FailureMessagesExplainThemselvesInsteadOfShowingNumbers()
    {
        Assert.Equal(
            "This process cannot work that material.",
            CraftFormat.Failure(CraftFailure.SubstrateRejected));
    }
}
