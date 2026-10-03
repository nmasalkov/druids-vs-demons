using UnityEngine;

/// <summary>
/// Improved Shock: bolts damage a standing enemy shield instead of being fully blocked, and
/// bolts left over once it breaks continue to the normal priority targets. See ShockResolver.
/// </summary>
[CreateAssetMenu(fileName = "ShockImprovement", menuName = "Game/Campaign/Rewards/Shock Improvement")]
public class ShockImprovementSO : ActionImprovementSO
{
    [Tooltip("Damage each Shock bolt deals to an enemy shield. Bolts keep hitting the shield until it breaks; the rest go to the targets behind it (Mage > Tank > Archer > Hero). See docs/ActionsAndSpells.md.")]
    public float shieldDamagePerBolt = 10f;
}
