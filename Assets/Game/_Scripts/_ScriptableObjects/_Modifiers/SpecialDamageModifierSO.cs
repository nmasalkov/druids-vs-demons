using UnityEngine;

/// <summary>
/// A custom damage rule carried by a creature. Assign one or more to
/// <see cref="CreatureSO.specialDamageModifiers"/> and every attack that creature makes runs
/// through them, in array order, after the BattleCry multiplier and once the target is known
/// (<c>AttacksResolver.ApplySpecialModifiers</c>).
///
/// The logic lives on the ScriptableObject itself — same shape as <see cref="RewardSO"/>'s
/// <c>Claim</c>/<c>IsOwned</c> and <see cref="ActionSO.CreateAndResolve"/>. Adding a new rule is
/// one subclass + one .asset; <c>AttacksResolver</c> never changes.
///
/// Not persisted: these hang off CreatureSO assets as direct references, so unlike ActionSO/RewardSO
/// they need no <c>id</c> and no catalog entry (CLAUDE.md rule 23 doesn't apply). See docs/Battle.md.
/// </summary>
public abstract class SpecialDamageModifierSO : ScriptableObject
{
    /// <summary>
    /// Takes the running damage value and returns the modified one. Modifiers are chained — the
    /// result feeds the next modifier in the array — so several stack multiplicatively. Return
    /// <paramref name="damage"/> unchanged when this rule doesn't apply to the given context.
    /// </summary>
    public abstract float ModifyDamage(in DamageContext context, float damage);
}
