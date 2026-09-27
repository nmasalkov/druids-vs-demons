using UnityEngine;

/// <summary>
/// Abstract base for a claimable reward card offered by a RewardEncounter. Persisted by id
/// (see RewardListSO/RunState), never by direct reference — mirrors ActionSO.id/GameCatalog.
/// See docs/Rewards.md.
/// </summary>
public abstract class RewardSO : ScriptableObject
{
    public string id;
    public string rewardName;
    [TextArea] public string description;
    public string typeLabel;
    [SerializeField] private Sprite icon;

    /// <summary>
    /// True if at most one copy of this reward can ever be owned — RewardDrawer excludes an
    /// already-owned unique reward from future draws. HpBoostRewardSO/BonusEnergyRewardSO are
    /// the only non-unique rewards (stackable, always re-offerable).
    /// </summary>
    public bool unique = true;

    /// <summary>
    /// icon if explicitly set, otherwise the linked action/creature's own cardSprite (most
    /// reward types borrow it rather than needing separate art).
    /// </summary>
    public Sprite EffectiveIcon => icon != null ? icon : FallbackIcon;
    protected virtual Sprite FallbackIcon => null;

    public abstract void Claim(RunState run);
    public abstract bool IsOwned(RunState run);
}
