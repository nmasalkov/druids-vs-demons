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

    public CreatureStats Stats(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, levelStats.Length - 1);
        return levelStats[index];
    }
}
