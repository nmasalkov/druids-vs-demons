using Game._Scripts.Spells;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBattleCry", menuName = "Game/Actions/Spells/BattleCry")]
public class BattleCrySO : SpellSO
{
    [Header("Own-side attack damage multiplier per level")]
    [Tooltip("Level 1 = Energy Tap, 2 = Energy Leak, 3 = Energy Drain. Index 0 = level 1.")]
    [SerializeField] private float[] buffMultiplierPerLevel = { 1.24f, 1.40f, 1.80f };

    [Header("Enemy-side attack damage multiplier per level")]
    [Tooltip("Level 1 = Energy Tap, 2 = Energy Leak, 3 = Energy Drain. Index 0 = level 1.")]
    [SerializeField] private float[] debuffMultiplierPerLevel = { 0.40f, 0.10f, 0.0f };

    public float GetBuffMultiplierForLevel(int level)
    {
        int idx = Mathf.Clamp(level - 1, 0, buffMultiplierPerLevel.Length - 1);
        return buffMultiplierPerLevel[idx];
    }

    public float GetDebuffMultiplierForLevel(int level)
    {
        int idx = Mathf.Clamp(level - 1, 0, debuffMultiplierPerLevel.Length - 1);
        return debuffMultiplierPerLevel[idx];
    }

    public override SpellResolver CreateResolver() => new BattleCryResolver();
    public override ActionAIScorer CreateAIScorer() => new BattleCryAIScorer();
}
