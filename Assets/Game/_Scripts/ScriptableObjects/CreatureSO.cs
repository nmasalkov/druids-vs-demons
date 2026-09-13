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
