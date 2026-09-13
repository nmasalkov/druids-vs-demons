using Game._Scripts.Creatures;
using UnityEngine;

/// <summary>
/// Deals bonus damage to Shields (the Shield spell's construct), leaving every other target
/// untouched. Carried by the ork roster — OrkMage/OrkTank/Kodo — matching their shield-breaking
/// hero avatar. See docs/Battle.md.
/// </summary>
[CreateAssetMenu(fileName = "ShieldBreaker", menuName = "Game/Modifiers/Shield Breaker")]
public class ShieldBreakerModifierSO : SpecialDamageModifierSO
{
    [Tooltip("Damage multiplier applied when the target is a Shield; other targets take normal " +
             "damage. 1.5 = +50%. Multiplies the already-BattleCry-adjusted value, so a buffed " +
             "creature's shield bonus scales with the buff.")]
    public float shieldDamageMultiplier = 1.5f;

    public override float ModifyDamage(in DamageContext context, float damage)
        => context.Target is Shield ? damage * shieldDamageMultiplier : damage;
}
