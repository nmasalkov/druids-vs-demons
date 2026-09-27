using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Master list of every reward card asset in the game. RunState's reward ids (statusRewardIds/
/// boostRewardIds) and RewardDrawer's pool are both resolved against this — same shape as
/// GameCatalog, one flat list + Find(id). See docs/Rewards.md.
/// </summary>
[CreateAssetMenu(fileName = "RewardListSO", menuName = "Game/Campaign/Reward List")]
public class RewardListSO : ScriptableObject
{
    public List<RewardSO> allRewards;

    public RewardSO Find(string id) => allRewards.FirstOrDefault(r => r.id == id);
}
