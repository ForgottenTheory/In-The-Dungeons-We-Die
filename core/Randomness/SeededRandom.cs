namespace Dungeons.Randomness;

/// <summary>
/// Deterministic <see cref="IRandomSource"/> backed by a seeded <see cref="Random"/>.
/// Given the same seed and call sequence it reproduces the same values, which
/// matters for tests, loot reproduction and future host-authoritative play.
/// </summary>
public sealed class SeededRandom : IRandomSource
{
    private readonly Random _random;

    public SeededRandom(int seed) => _random = new Random(seed);

    public double NextDouble() => _random.NextDouble();

    public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
