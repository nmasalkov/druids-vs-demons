using System;
using UnityEngine;

[Serializable]
public struct CreatureStats
{
    public float damage;
    public float health;
    public int numberOfAttacks;
}

[CreateAssetMenu(fileName = "NewCreature", menuName = "Game/Actions/Creature")]
public class CreatureSO : ActionSO
{
    public GameObject creaturePrefab;

    [Header("Stats per level (index 0 = level 1, etc.)")]
    public CreatureStats[] levelStats = new CreatureStats[]
    {
        new() { damage = 10f, health = 100f, numberOfAttacks = 1 },
        new() { damage = 15f, health = 130f, numberOfAttacks = 1 },
        new() { damage = 20f, health = 170f, numberOfAttacks = 1 },
        new() { damage = 30f, health = 220f, numberOfAttacks = 1 },
    };

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
