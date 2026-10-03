using UnityEngine;

/// <summary>
/// Abstract base for a reward that changes how an action behaves rather than scaling a number
/// (e.g. Shock bolts hurting shields). Each concrete subclass is the key its resolver checks via
/// ActionResolver.IsImproved&lt;T&gt;(caster, out T) and carries its own tunables. Player side only.
/// See docs/Rewards.md.
/// </summary>
public abstract class ActionImprovementSO : BoostSO
{
    [Tooltip("The nuke/spell this improvement modifies. Used for the reward icon fallback (its cardSprite); the resolver finds the improvement by type, not by this reference.")]
    public ActionSO action;

    protected override Sprite FallbackIcon => action.cardSprite;
}
