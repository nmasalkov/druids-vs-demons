using Game._Scripts.Creatures;
using Game._Scripts.Spells;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharm", menuName = "Game/Actions/Spells/Charm")]
public class CharmSO : SpellSO
{
    [Header("Success chance per level")]
    [Tooltip("Multiplied by the number of alive enemy creatures, then clamped to 1. Index 0 = level 1.")]
    [SerializeField] private float[] chancePerLevel = { 0.12f, 0.21f, 0.35f };

    [Header("Target strength")]
    [Tooltip("Strength = currentHp + creatureLevel * this. Candidates are ranked by strength to pick the weakest/median/strongest target for level 1/2/3.")]
    [SerializeField] private float levelStrengthWeight = 100f;

    public float GetChanceForLevel(int level)
    {
        int idx = Mathf.Clamp(level - 1, 0, chancePerLevel.Length - 1);
        return chancePerLevel[idx];
    }

    public float GetStrength(Creature c) => c.Health.CurrentHealth + c.Experience.Level * levelStrengthWeight;

    public override SpellResolver CreateResolver() => new CharmResolver();
    public override ActionAIScorer CreateAIScorer() => new CharmAIScorer();
}
