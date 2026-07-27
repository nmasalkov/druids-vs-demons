using UnityEngine;

/// <summary>
/// Post-fight encounter: a full-screen tint + a claimable energy reward. Formerly
/// FightSO.energyReward, granted automatically on victory — now an explicit encounter the
/// campaign designer places in the EncounterListSO wherever a reward should happen. Played by
/// EncounterPlayer/RewardEncounter. See docs/Encounters.md.
/// </summary>
[CreateAssetMenu(fileName = "RewardPick", menuName = "Game/Campaign/Reward Pick")]
public class RewardPickSO : EncounterSO
{
    public int energyReward;
}
