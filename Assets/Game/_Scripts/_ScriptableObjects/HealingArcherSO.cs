using UnityEngine;

/// <summary>
/// An archer that shoots twice per battle: a pre-phase support volley healing random wounded allies
/// (other creatures or the hero, never itself or a Shield) — one heal per shot, each for
/// a fraction of its per-shot damage — then its regular attack. Heals are planned before any damage in
/// AttacksResolver.ResolveHeals and land first on screen (the battle pre-phase), animated by
/// SplitShotArcherAnimator. See docs/Battle.md "Healing shots".
/// </summary>
[CreateAssetMenu(fileName = "NewHealingArcher", menuName = "Game/Actions/Creatures/Healing Archer")]
public class HealingArcherSO : ArcherSO
{
    [Header("Healing")]
    [Tooltip("Heal per shot as a fraction of this archer's per-shot damage, per level (index 0 = level 1, " +
             "clamped like levelStats). 0.75 = 75%. Based on the damage after the archer class reward boost " +
             "and BattleCry (so both scale healing too; Energy Drain/Shock cancel it), before crits and " +
             "special modifiers. One heal per numberOfAttacks, each on a random OTHER ally below max HP; a shot " +
             "with no wounded ally heals nothing. Heals play in the battle pre-phase, before the regular attack. See docs/Battle.md \"Healing shots\".")]
    public float[] healFractions = { 0.75f, 0.75f, 0.75f, 0.75f };

    public float HealFraction(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, healFractions.Length - 1);
        return healFractions[index];
    }
}
