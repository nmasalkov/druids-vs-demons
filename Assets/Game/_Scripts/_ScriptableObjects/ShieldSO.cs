using Game._Scripts.Creatures;
using Game._Scripts.Spells;
using UnityEngine;

[CreateAssetMenu(fileName = "NewShield", menuName = "Game/Actions/Spells/Shield")]
public class ShieldSO : SpellSO
{
    [Header("Stats")]
    [Tooltip("Max HP per level. Index 0 = level 1, 1 = level 2, 2 = level 3.")]
    [SerializeField] private float[] hpPerLevel = { 15f, 27f, 56f };

    [Tooltip("Heal applied when rolled level <= existing shield level. Same indexing.")]
    [SerializeField] private float[] healPerLevel = { 7f, 13f, 27f };

    [Header("Prefabs")]
    [SerializeField] private Shield playerShieldPrefab;
    [SerializeField] private Shield enemyShieldPrefab;

    public float GetHpForLevel(int level)
    {
        int idx = Mathf.Clamp(level - 1, 0, hpPerLevel.Length - 1);
        return hpPerLevel[idx];
    }

    public float GetHealForLevel(int level)
    {
        int idx = Mathf.Clamp(level - 1, 0, healPerLevel.Length - 1);
        return healPerLevel[idx];
    }

    public Shield GetPrefab(bool isPlayer) => isPlayer ? playerShieldPrefab : enemyShieldPrefab;

    public override SpellResolver CreateResolver() => new ShieldResolver();
    public override ActionAIScorer CreateAIScorer() => new ShieldAIScorer();
}

