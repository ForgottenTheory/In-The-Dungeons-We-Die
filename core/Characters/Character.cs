using Dungeons.Characters.Composition;
using Dungeons.Characters.Rules;

namespace Dungeons.Characters;

/// <summary>
/// A runtime character instance: an immutable <see cref="CharacterBlueprint"/> plus
/// mutable resource pools. Its <see cref="EffectiveAttributes"/> combine the static
/// base attributes with whatever the attached rule hooks currently contribute, so
/// the same character can behave differently as its Health changes.
/// </summary>
public sealed class Character
{
    public Character(CharacterBlueprint blueprint)
    {
        Blueprint = blueprint;
        Health = new ResourcePool(ResourceType.Health, blueprint.MaxHealth);
        Mana = new ResourcePool(ResourceType.Mana, blueprint.MaxMana);
        Stamina = new ResourcePool(ResourceType.Stamina, blueprint.MaxStamina);
    }

    public CharacterBlueprint Blueprint { get; }

    public ResourcePool Health { get; }
    public ResourcePool Mana { get; }
    public ResourcePool Stamina { get; }

    public string DisplayName => Blueprint.DisplayName;

    /// <summary>Static attributes before dynamic rule bonuses.</summary>
    public AttributeSet BaseAttributes => Blueprint.BaseAttributes;

    /// <summary>Base attributes plus all currently-active rule bonuses.</summary>
    public AttributeSet EffectiveAttributes
    {
        get
        {
            var snapshot = Snapshot();
            var result = Blueprint.BaseAttributes;
            foreach (var rule in Blueprint.Rules)
            {
                foreach (var bonus in rule.GetDynamicAttributeBonuses(snapshot))
                    result = result.Add(bonus.Attribute, bonus.Amount);
            }

            return result;
        }
    }

    public ResourcePool Resource(ResourceType type) => type switch
    {
        ResourceType.Health => Health,
        ResourceType.Mana => Mana,
        ResourceType.Stamina => Stamina,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>The pool for the primary resource declared by the base class.</summary>
    public ResourcePool PrimaryResource => Resource(Blueprint.PrimaryResource);

    /// <summary>Applies Health damage. Returns the amount actually removed.</summary>
    public int TakeDamage(int amount) => Health.Reduce(amount);

    /// <summary>Restores Health. Returns the amount actually healed.</summary>
    public int Heal(int amount) => Health.Restore(amount);

    /// <summary>Refills every resource to its maximum.</summary>
    public void RestoreAll()
    {
        Health.Fill();
        Mana.Fill();
        Stamina.Fill();
    }

    public CharacterSnapshot Snapshot() => new()
    {
        BaseAttributes = Blueprint.BaseAttributes,
        Health = Health.Current,
        MaxHealth = Health.Max,
        Mana = Mana.Current,
        MaxMana = Mana.Max,
        Stamina = Stamina.Current,
        MaxStamina = Stamina.Max,
    };
}
