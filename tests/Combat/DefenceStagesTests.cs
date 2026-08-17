using Dungeons.Combat;
using Dungeons.Modifiers;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// R4c — the defence stages that arrived with their affix families: Evade (untelegraphed only,
/// D-07), per-lane avoidance (packet negation), and flat lane penetration after the cap
/// (§4.2 step 6 — it eats overcap; exposure, summed before the cap, does not).
/// </summary>
public class DefenceStagesTests
{
    private static HitPipeline PipelineWith(double roll, params ModifierContribution[] contributions)
    {
        var bus = new Dungeons.Events.GameEventBus();
        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), bus, () => 0);
        var modifiers = new CombatantModifiers(
            TestPaths.LoadStore<ModifierKeyDefinition>("modifier_keys"),
            isOwner: c => c.Team == CombatTeam.Player,
            buildModifiers: () => contributions,
            statuses,
            new Dungeons.Characters.GaugeController(
                Array.Empty<Dungeons.Characters.Composition.GaugeDefinition>()));

        return new HitPipeline(new FakeRandom(roll), modifiers);
    }

    private static Hit Attack(Combatant source, Combatant target, bool untelegraphed, params Packet[] packets) => new()
    {
        Source = source,
        Target = target,
        Name = "Test Attack",
        Packets = packets,
        Untelegraphed = untelegraphed,
    };

    private static Combatant Attacker() =>
        Enemy("A", 50, Attrs(), Move("move.strike", DamageType.Magic, 8, 2, 8, 15));

    // ---- Evade (D-07) ---------------------------------------------------------------------------

    [Fact]
    public void EvadeAvoidsUntelegraphedHitsOnly()
    {
        var target = Player(attrs: Attrs());
        var evade = new ModifierContribution(ModifierKeys.EvadeChance, 0.15, "ghoststep");

        // Roll 0.01 < 15% → the untelegraphed hit is avoided outright…
        var sneak = PipelineWith(0.01, evade).Resolve(
            Attack(Attacker(), target, untelegraphed: true, new Packet(DamageType.Slashing, 20)), 0);
        Assert.True(sneak.Avoided);
        Assert.Equal(AvoidedVia.Evade, sneak.AvoidedBy);

        // …but the telegraphed hit never rolls evade at all: the skill test stays mandatory.
        var telegraphed = PipelineWith(0.01, evade).Resolve(
            Attack(Attacker(), target, untelegraphed: false, new Packet(DamageType.Slashing, 20)), 0);
        Assert.False(telegraphed.Avoided);
    }

    [Fact]
    public void EvadeNeverRollsWithoutASource()
    {
        // Chance 0 → no RNG draw, no avoidance — R4a/R4c change nothing for existing content.
        var target = Player(attrs: Attrs());
        var result = PipelineWith(0.01).Resolve(
            Attack(Attacker(), target, untelegraphed: true, new Packet(DamageType.Slashing, 20)), 0);

        Assert.False(result.Avoided);
    }

    // ---- Lane avoidance --------------------------------------------------------------------------

    [Fact]
    public void LaneAvoidanceNegatesPacketsInItsLaneOnly()
    {
        var target = Player(attrs: Attrs());
        var veil = new ModifierContribution(
            ModifierKeys.AvoidLane, 0.25, "storm veil", new ModifierScope(ScopeDimensions.Lane, "charge"));

        var result = PipelineWith(0.01, veil).Resolve(
            Attack(Attacker(), target, untelegraphed: false,
                new Packet(DamageType.Magic, DamageAspects.Charge, 20),
                new Packet(DamageType.Magic, 10)),
            0);

        // The charge packet is negated; the plain magic packet still lands.
        Assert.False(result.Avoided);
        Assert.True(result.Amount > 0 && result.Amount < 15, $"charge negated, magic lands: {result.Amount}");
        Assert.Contains(result.Log.Lines, l => l.Stage == HitStages.Negate);
    }

    // ---- Parry (R4c-2, D-26) ----------------------------------------------------------------------

    [Fact]
    public void ParryAvoidsInsideItsThreeTickWindow()
    {
        var target = Player(attrs: Attrs());
        target.ParryUntilTick = 2; // raised at tick 0, window = 3 ticks

        var inWindow = PipelineWith(0.99).Resolve(
            Attack(Attacker(), target, untelegraphed: false, new Packet(DamageType.Slashing, 20)), currentTick: 2);
        Assert.True(inWindow.Avoided);
        Assert.Equal(AvoidedVia.Parry, inWindow.AvoidedBy);

        var late = PipelineWith(0.99).Resolve(
            Attack(Attacker(), target, untelegraphed: false, new Packet(DamageType.Slashing, 20)), currentTick: 3);
        Assert.False(late.Avoided);
    }

    // ---- Penetration (§4.2 step 6) ----------------------------------------------------------------

    [Fact]
    public void PenetrationEatsThroughOvercapButExposureDoesNot()
    {
        // Build contributions land on the player (isOwner), so the PLAYER attacks here — the
        // same direction a Rending weapon works in play. The enemy defends overcapped:
        // raw 90% heat resistance, capped to 75%.
        var attacker = Player(attrs: Attrs());
        var target = Enemy("Warden", 200, Attrs(),
            Move("move.strike", DamageType.Magic, 8, 2, 8, 15),
            armor: new ArmorProfile
            {
                Armor = 0,
                Resistances = new Dictionary<string, double> { [DamageLanes.Heat] = 0.90 },
            });

        double DamageWith(params ModifierContribution[] mods) =>
            PipelineWith(0.99, mods).Resolve(
                Attack(attacker, target, untelegraphed: false,
                    new Packet(DamageType.Magic, DamageAspects.Heat, 40)),
                0).Amount;

        var baseline = DamageWith();

        // Penetration applies AFTER the cap: 75% − 20% = 55% → visibly more damage. (Exposure —
        // a negative resist contribution before the cap — would be absorbed by the overcap, but
        // enemy-side debuffs ride statuses rather than build contributions, so that half is
        // pinned at the resolution level: capped 0.90→0.75 with or without −0.10.)
        Assert.Equal(
            Combatant.CapResistance(0.90),
            Combatant.CapResistance(0.90 - 0.10)); // overcap absorbs exposure

        var penetrated = DamageWith(new ModifierContribution(
            ModifierKeys.PenLane, 0.20, "rending", new ModifierScope(ScopeDimensions.Lane, "heat")));
        Assert.True(penetrated > baseline, $"pen must beat overcap ({baseline} → {penetrated})");
    }
}
