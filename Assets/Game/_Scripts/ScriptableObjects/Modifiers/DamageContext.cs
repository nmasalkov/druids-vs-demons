using Game._Scripts.Creatures;

/// <summary>
/// Bundle of "who is hitting whom" passed to <see cref="SpecialDamageModifierSO.ModifyDamage"/>.
/// Mirrors <see cref="ActionContext"/>: each concrete modifier reads only the fields it needs
/// (ShieldBreaker only tests <see cref="Target"/>'s type). Built fresh per planned hit inside
/// <c>AttacksResolver.ResolveTeam</c> — a readonly struct passed by <c>in</c>, so it never
/// allocates, and new fields can be added later without touching existing modifiers.
/// See docs/Battle.md.
/// </summary>
public readonly struct DamageContext
{
    /// <summary>The creature dealing the damage. Its CreatureSO owns the modifier list being run.</summary>
    public readonly Creature Attacker;

    /// <summary>What is being hit this shot — a Creature, a Hero, or a Shield.</summary>
    public readonly Targetable Target;

    public DamageContext(Creature attacker, Targetable target)
    {
        Attacker = attacker;
        Target = target;
    }
}
