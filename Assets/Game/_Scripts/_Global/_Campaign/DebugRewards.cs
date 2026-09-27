using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Debug-only reward-roll override: check "Roll On Next Reward" and assign slot1/2/3 to force
/// exactly what the next RewardEncounter offers, instead of RewardDrawer's random draw — lets you
/// deterministically reproduce/inspect a specific reward combo. Auto-clears itself once consumed
/// (it's "next", singular, not sticky). Hijacks the flow at the earliest possible point
/// (RewardEncounter.SpawnCards, right where it would otherwise call RewardDrawer.DrawThree) per
/// CLAUDE.md's "debug tools hijack as early as possible" rule. Same cross-scene duplicate-guard
/// singleton pattern as CampaignDebugTool, placed as a sibling component on the same
/// CampaignProgress GameObject (root-level, so DontDestroyOnLoad actually works — see that
/// GameObject's history) in both BattleScene/MapScene. See docs/Rewards.md.
/// </summary>
public class DebugRewards : MonoBehaviour
{
    public static DebugRewards Instance { get; private set; }

    [Tooltip("When checked, the next RewardEncounter offers exactly slot1/2/3 instead of a random draw. Clears itself once consumed.")]
    public bool rollOnNextReward;
    public RewardSO slot1;
    public RewardSO slot2;
    public RewardSO slot3;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Returns the forced 3-card draw and clears the override, or null if none is set —
    /// called once per RewardEncounter.Play().</summary>
    public List<RewardSO> ConsumeOverrideDraw()
    {
        if (!rollOnNextReward) return null;
        rollOnNextReward = false;
        return new List<RewardSO> { slot1, slot2, slot3 };
    }
}
