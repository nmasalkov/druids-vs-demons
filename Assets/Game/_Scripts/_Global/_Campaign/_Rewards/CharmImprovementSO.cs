using UnityEngine;

/// <summary>
/// Improved Charm: a failed charm may shock its target instead of doing nothing. See CharmResolver.
/// </summary>
[CreateAssetMenu(fileName = "CharmImprovement", menuName = "Game/Campaign/Rewards/Charm Improvement")]
public class CharmImprovementSO : ActionImprovementSO
{
    [Tooltip("Chance (0-1) that a failed Charm shocks its target creature. Rolled once in the resolver so the animated and instant paths agree. See docs/ActionsAndSpells.md.")]
    [Range(0f, 1f)] public float shockOnFailChance = 0.5f;
}
