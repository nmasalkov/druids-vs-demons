using Game._Scripts.Creatures;
using Game._Scripts.Spells;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharm", menuName = "Game/Actions/Spells/Charm")]
public class CharmSO : SpellSO
{
    [Header("Success chance per level")]
    [Tooltip("Multiplied by the number of alive enemy creatures, then clamped to 1. Index 0 = level 1.")]
    [SerializeField] private float[] chancePerLevel = { 0.12f, 0.21f, 0.35f };

    [Header("Creature advantage penalty")]
    [Tooltip("How much each creature the caster out-numbers the charmed side by cuts the success " +
             "chance, as a fraction. 0.25 = -25% per extra creature, applied linearly and AFTER " +
             "the base chance has been clamped to 1 — so a caster holding 4 creatures against 2 " +
             "lands on 1.0 * (1 - 0.25 * 2) = 0.5. Stops a side that's already winning the board " +
             "from snowballing on steals. 0 disables the penalty. The AI's CharmAIScorer scales " +
             "its own priority by the same number, so tuning this moves both together. See " +
             "docs/ActionsAndSpells.md and docs/AI.md.")]
    [SerializeField] private float chancePenaltyPerExtraCreature = 0.25f;

    [Header("Target strength")]
    [Tooltip("Strength = currentHp + creatureLevel * this. Candidates are ranked by strength to pick the weakest/median/strongest target for level 1/2/3.")]
    [SerializeField] private float levelStrengthWeight = 100f;

    public float GetChanceForLevel(int level)
    {
        int idx = Mathf.Clamp(level - 1, 0, chancePerLevel.Length - 1);
        return chancePerLevel[idx];
    }

    /// <summary>
    /// Multiplier for out-numbering the side being charmed — 1 when the caster has no creature
    /// advantage, falling by <c>chancePenaltyPerExtraCreature</c> for each creature it leads by,
    /// floored at 0. Being behind grants no bonus (the multiplier never exceeds 1).
    ///
    /// Shared by CharmResolver (the real roll) and CharmAIScorer (the AI's priority for casting
    /// it), so the odds and the AI's appetite for those odds can't drift apart — one tunable,
    /// one formula. See docs/AI.md.
    /// </summary>
    public float GetCreatureAdvantageMultiplier(int casterCreatureCount, int charmedSideCreatureCount)
    {
        int advantage = Mathf.Max(0, casterCreatureCount - charmedSideCreatureCount);
        return Mathf.Max(0f, 1f - chancePenaltyPerExtraCreature * advantage);
    }

    public float GetStrength(Creature c) => c.Health.CurrentHealth + c.Experience.Level * levelStrengthWeight;

    public override SpellResolver CreateResolver() => new CharmResolver();
    public override ActionAIScorer CreateAIScorer() => new CharmAIScorer(this);
}
