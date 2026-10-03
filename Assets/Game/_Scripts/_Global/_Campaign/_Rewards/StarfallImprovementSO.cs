using UnityEngine;

/// <summary>
/// Improved Starfall: after the main volley, extra stars strike random enemies still alive.
/// See StarfallResolver.
/// </summary>
[CreateAssetMenu(fileName = "StarfallImprovement", menuName = "Game/Campaign/Rewards/Starfall Improvement")]
public class StarfallImprovementSO : ActionImprovementSO
{
    [Tooltip("Extra stars fired after the main volley, each at a random enemy that survives the volley (same damage as a normal star). None fire if nothing survives. See docs/ActionsAndSpells.md.")]
    [Min(0)] public int extraStrikes = 1;
}
