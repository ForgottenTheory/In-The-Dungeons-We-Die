using Dungeons.Randomness;
using Dungeons.Rules;

namespace Dungeons.Combat;

/// <summary>
/// Shared plumbing for the combat effect handlers.
///
/// <para>Every one of them does the same three things: resolve who it acts on, act, and hand
/// <see cref="EffectInvocation.Context"/> to combat so the causal chain survives. That third one
/// is the one that breaks silently if forgotten — the chain restarts at depth 0 and the proc
/// budget stops bounding anything — so it lives here rather than in six places.</para>
/// </summary>
public abstract class CombatEffectHandler : IEffectHandler
{
    protected CombatEffectHandler(CombatEncounter encounter, EffectTargetResolver targets)
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        Targets = targets ?? throw new ArgumentNullException(nameof(targets));
    }

    protected CombatEncounter Encounter { get; }

    protected EffectTargetResolver Targets { get; }

    public abstract string Kind { get; }

    public abstract void Execute(EffectInvocation invocation);
}

/// <summary>Flat damage from a rule. Not a swing — see <see cref="CombatEncounter.DealEffectDamage"/>.</summary>
public sealed class DamageEffectHandler : CombatEffectHandler
{
    public DamageEffectHandler(CombatEncounter encounter, EffectTargetResolver targets)
        : base(encounter, targets) { }

    public override string Kind => RuleVocabulary.Damage;

    public override void Execute(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        foreach (var target in Targets.Resolve(invocation))
        {
            Encounter.DealEffectDamage(
                Encounter.Player, target, invocation.Magnitude, invocation.Source, invocation.Context);
        }
    }
}

/// <summary>Damage to everything hostile. The selector picks the centre; positioning decides the
/// radius, and there is no positioning yet (<see cref="EffectTargetResolver.ResolveArea"/>).</summary>
public sealed class AreaDamageEffectHandler : CombatEffectHandler
{
    public AreaDamageEffectHandler(CombatEncounter encounter, EffectTargetResolver targets)
        : base(encounter, targets) { }

    public override string Kind => RuleVocabulary.AreaDamage;

    public override void Execute(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        foreach (var target in Targets.ResolveArea(invocation))
        {
            Encounter.DealEffectDamage(
                Encounter.Player, target, invocation.Magnitude, invocation.Source, invocation.Context);
        }
    }
}

public sealed class HealEffectHandler : CombatEffectHandler
{
    public HealEffectHandler(CombatEncounter encounter, EffectTargetResolver targets)
        : base(encounter, targets) { }

    public override string Kind => RuleVocabulary.Heal;

    public override void Execute(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        foreach (var target in Targets.Resolve(invocation))
            Encounter.HealTarget(target, invocation.Magnitude, invocation.Source, invocation.Context);
    }
}

/// <summary>
/// Applies a status. <see cref="EffectSpec.Text"/> is the status id and the validator has already
/// proved it resolves, so a miss here is a bug rather than a content typo.
/// </summary>
public sealed class ApplyStatusEffectHandler : CombatEffectHandler
{
    public ApplyStatusEffectHandler(CombatEncounter encounter, EffectTargetResolver targets)
        : base(encounter, targets) { }

    public override string Kind => RuleVocabulary.ApplyStatus;

    public override void Execute(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var statusId = invocation.Effect.Text;
        if (string.IsNullOrWhiteSpace(statusId))
            return;

        foreach (var target in Targets.Resolve(invocation))
        {
            Encounter.ApplyStatus(
                target, statusId, invocation.Source, invocation.Magnitude,
                invocation.Effect.DurationTicks, invocation.Context);
        }
    }
}

/// <summary>
/// Fills a gauge — every authored use of this effect names one.
///
/// <para>The target is deliberately <b>not</b> the selector's answer: a gauge belongs to the
/// character running the build, so Galvanic's Charge accumulates on you whether the event that
/// fed it was your hit or the enemy's. Routing it through the selector would put Charge on the
/// goblin.</para>
/// </summary>
public sealed class GrantResourceEffectHandler : CombatEffectHandler
{
    public GrantResourceEffectHandler(CombatEncounter encounter, EffectTargetResolver targets)
        : base(encounter, targets) { }

    public override string Kind => RuleVocabulary.GrantResource;

    public override void Execute(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        Encounter.GrantResource(
            Encounter.Player, invocation.Effect.Text, invocation.Magnitude, invocation.Context);
    }
}

/// <summary>
/// Grants a modifier for a span of ticks — the third most-authored effect kind (14 uses).
///
/// <para><see cref="EffectSpec.Text"/> is the modifier key and the validator has already proved
/// it is registered. <see cref="EffectSpec.DurationTicks"/> of 0 lasts the encounter.</para>
/// </summary>
public sealed class GrantModifierEffectHandler : CombatEffectHandler
{
    public GrantModifierEffectHandler(CombatEncounter encounter, EffectTargetResolver targets)
        : base(encounter, targets) { }

    public override string Kind => RuleVocabulary.GrantModifier;

    public override void Execute(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var key = invocation.Effect.Text;
        if (string.IsNullOrWhiteSpace(key) || Encounter.Modifiers is null)
            return;

        foreach (var target in Targets.Resolve(invocation))
        {
            Encounter.Modifiers.Timed.Grant(
                target, key, invocation.Magnitude, invocation.Source,
                invocation.Effect.DurationTicks, Encounter.CurrentTick);
        }
    }
}

/// <summary>Cuts a committed action short. The phase it cut is tagged by combat, so content can
/// tell "stopped them before they swung" from "stopped them mid-swing".</summary>
public sealed class InterruptEffectHandler : CombatEffectHandler
{
    public InterruptEffectHandler(CombatEncounter encounter, EffectTargetResolver targets)
        : base(encounter, targets) { }

    public override string Kind => RuleVocabulary.Interrupt;

    public override void Execute(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        foreach (var target in Targets.Resolve(invocation))
            Encounter.Interrupt(target, invocation.Source, invocation.Context);
    }
}

/// <summary>Registers every combat-owned effect handler on a rule engine.</summary>
public static class CombatEffects
{
    /// <summary>
    /// The seven kinds combat owns. The rest of <see cref="RuleVocabulary.Effects"/> belongs to
    /// systems that do not exist yet — <c>spawnEntity</c>, <c>grantItem</c>, <c>reposition</c>,
    /// <c>revealInfo</c> — and stays in <c>Unhandled</c>, which is the point of that list.
    /// </summary>
    public static TriggerRuleEngine RegisterCombatHandlers(
        this TriggerRuleEngine engine, CombatEncounter encounter, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var targets = new EffectTargetResolver(encounter, random);

        return engine
            .Register(new DamageEffectHandler(encounter, targets))
            .Register(new AreaDamageEffectHandler(encounter, targets))
            .Register(new HealEffectHandler(encounter, targets))
            .Register(new ApplyStatusEffectHandler(encounter, targets))
            .Register(new GrantResourceEffectHandler(encounter, targets))
            .Register(new GrantModifierEffectHandler(encounter, targets))
            .Register(new InterruptEffectHandler(encounter, targets));
    }
}
