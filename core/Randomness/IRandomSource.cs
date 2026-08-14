namespace Dungeons.Randomness;

/// <summary>
/// Abstraction over random number generation so Domain systems stay deterministic
/// and testable. Realm generation, loot and bonus-output rolls should draw from an
/// injected source rather than a scattered global RNG (docs/architecture.md §26).
/// </summary>
public interface IRandomSource
{
    /// <summary>Returns a value in [0, 1).</summary>
    double NextDouble();

    /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
    int NextInt(int minInclusive, int maxExclusive);
}
