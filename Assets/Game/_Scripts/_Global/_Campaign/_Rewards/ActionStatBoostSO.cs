using UnityEngine;

/// <summary>
/// +percentage to one nuke/spell's main balance number (Fire Magic damage, Shield HP/heal,
/// Battle Cry's damage-bonus part). Read by resolvers via ActionResolver.Boosted /
/// RewardBonuses.ApplyActionBonus — player side only. See docs/Rewards.md.
/// </summary>
[CreateAssetMenu(fileName = "ActionStatBoost", menuName = "Game/Campaign/Rewards/Action Stat Boost")]
public class ActionStatBoostSO : BoostSO
{
    [Tooltip("The nuke/spell this boost strengthens. Its cardSprite doubles as this reward's icon when no explicit icon is set.")]
    public ActionSO action;

    [Tooltip("Fraction added to the action's boosted number (0.2 = +20%). For Battle Cry it scales only the bonus part of the buff multiplier (x1.24 with 0.6 -> x1.384). Player side only. See docs/Rewards.md.")]
    [Range(0f, 1f)] public float percentage = 0.2f;

    protected override Sprite FallbackIcon => action.cardSprite;
}
