using System;
using UnityEngine;

[Serializable]
public struct CreatureStats
{
    [Tooltip("Damage dealt per single hit at this level, before the BattleCry multiplier and any special damage modifiers (see docs/Battle.md).")]
    public float damage;

    [Tooltip("Max HP at this level. Read by Creature.GetMaxHealth(), which also applies claimed creature-boost rewards on top (see docs/Rewards.md).")]
    public float health;

    [Tooltip("How many separate hits this creature lands per battle turn. Each hit re-picks its target, so a multi-hit attacker can finish one target and roll onto the next.")]
    public int numberOfAttacks;

    [Tooltip("Per-hit damage assumed by the pre-battle firepower estimate (comeback rigging, AI reroll budget, " +
             "BalanceTool HUD) when this creature's real damage is 0 — e.g. a counter-tank or a future healer " +
             "that still pulls its weight without attacking. Ignored when damage > 0; never used in real combat. " +
             "See docs/Battle.md.")]
    public float nominalDamage;
}

[CreateAssetMenu(fileName = "NewCreature", menuName = "Game/Actions/Creature")]
public class CreatureSO : ActionSO
{
    [Tooltip("Prefab instantiated into a UnitSlot when this creature is summoned. Must carry a Creature component whose Data points back at this asset.")]
    public GameObject creaturePrefab;

    [Header("Stats per level (index 0 = level 1, etc.)")]
    [Tooltip("Damage / HP / attack count per creature level, indexed level - 1 and clamped. Levels come from Experience (see docs/Experience.md), not from how many symbols were rolled.")]
    public CreatureStats[] levelStats = new CreatureStats[]
    {
        new() { damage = 10f, health = 100f, numberOfAttacks = 1 },
        new() { damage = 15f, health = 130f, numberOfAttacks = 1 },
        new() { damage = 20f, health = 170f, numberOfAttacks = 1 },
        new() { damage = 30f, health = 220f, numberOfAttacks = 1 },
    };

    [Header("Special damage modifiers")]
    [Tooltip("Optional custom damage rules applied to every attack this creature makes — after the " +
             "BattleCry multiplier and once the target is known. Each entry takes the running damage " +
             "value and returns the next, so several stack multiplicatively. Leave empty for most " +
             "creatures. See docs/Battle.md.")]
    public SpecialDamageModifierSO[] specialDamageModifiers = new SpecialDamageModifierSO[0];

    [Header("Critical strike")]
    [Tooltip("Chance, 0–100%, that a single hit is a critical strike. Rolled separately for every hit " +
             "(a multi-hit archer can crit some hits and not others). 0 = never crits. See docs/Battle.md.")]
    [Range(0, 100)] public int critChancePercent;

    [Tooltip("Extra damage a critical hit deals, in percent: 50 = +50% (×1.5), 100 = double. Always " +
             "applied last — on top of the BattleCry multiplier and every special damage modifier. " +
             "See docs/Battle.md.")]
    [Min(0)] public int critDamageBonusPercent;

    /// <summary>Damage multiplier a critical hit applies (1.5 for +50%).</summary>
    public float CritDamageMultiplier => 1f + critDamageBonusPercent / 100f;

    /// <summary>Average damage multiplier crits add over many hits (1.1 for 20% × +50%) — used by
    /// the pre-battle firepower estimate, which can't roll dice.</summary>
    public float ExpectedCritMultiplier => 1f + critChancePercent / 100f * (critDamageBonusPercent / 100f);

    [Header("Targeting")]
    [Tooltip("Taunt: enemy creature attacks hit this creature before any non-preferred creature, ahead " +
             "of the normal Mage → Archer → Tank order. A standing shield still comes first. Only " +
             "creature attacks honor it — nukes keep their own order. Several preferred creatures fall " +
             "back to the class order among themselves. See docs/Battle.md (resolution step 2, targeting priority).")]
    public bool preferredTarget;

    [Header("Experience")]
    [Tooltip("XP reward for killing this creature at each level (index 0 = level 1)")]
    public int[] experienceReward = { 30, 60, 120, 240 };

    [Tooltip("Cumulative XP needed for each level (index 0 = level 1, index 1 = level 2, etc.)")]
    public int[] xpThresholds = { 0, 120, 320, 600 };

    [Tooltip("Heal amount when a matching roll doesn't promote (index 0 = single card, index 1 = pair, etc.)")]
    public float[] healAmounts = { 3f, 7f, 10f, 24f };

    public CreatureStats Stats(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, levelStats.Length - 1);
        return levelStats[index];
    }

    /// <summary>Per-hit damage the firepower estimate should count: the real damage, or
    /// <see cref="CreatureStats.nominalDamage"/> for a creature that doesn't attack.</summary>
    public float EstimatedDamage(CreatureStats stats) => stats.damage > 0f ? stats.damage : stats.nominalDamage;

    public int GetExperienceReward(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, experienceReward.Length - 1);
        return experienceReward[index];
    }

    public int GetLevelForXp(int totalXp)
    {
        // Start from highest level, find the first threshold that totalXp meets
        // Skip index 0 (level 1 threshold is always 0)
        for (int i = xpThresholds.Length - 1; i >= 1; i--)
        {
            if (totalXp >= xpThresholds[i])
                return i + 1;
        }
        return 1;
    }
}
