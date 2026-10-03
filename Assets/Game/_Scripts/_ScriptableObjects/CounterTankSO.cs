using UnityEngine;

/// <summary>
/// A tank that never attacks on its own turn (levelStats damage 0) and instead counterattacks every
/// creature hit that lands on it, for a fraction of that hit's damage. Resolved as a post-pass in
/// AttacksResolver.ResolveCounters, animated by CounterTankAnimator. See docs/Battle.md "Counterattacks".
/// </summary>
[CreateAssetMenu(fileName = "NewCounterTank", menuName = "Game/Actions/Creatures/Counter Tank")]
public class CounterTankSO : TankSO
{
    [Header("Counterattack")]
    [Tooltip("Fraction of each incoming creature hit's final damage (after the attacker's crit) this tank " +
             "sends back at the attacker (or at the Shield guarding the attacker's side while it's up), per level " +
             "(index 0 = level 1, clamped like levelStats). 0.35 = 35%. Flat: no tank class reward boost, " +
             "BattleCry/Energy Drain, special modifiers or crit apply to the counter. Nukes never trigger it. " +
             "See docs/Battle.md \"Counterattacks\".")]
    public float[] counterDamageFractions = { 0.35f, 0.38f, 0.41f, 0.45f };

    public float CounterFraction(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, counterDamageFractions.Length - 1);
        return counterDamageFractions[index];
    }
}
