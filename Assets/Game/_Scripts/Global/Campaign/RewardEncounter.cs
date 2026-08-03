using System;
using System.Collections.Generic;

/// <summary>
/// Post-fight encounter backend: grants the guaranteed energy reward (RewardPickSO.energyReward)
/// immediately, then offers 3 reward cards drawn by RewardDrawer — pick one, then Confirm to advance.
/// Pure data/logic — no UI reference of any kind. Presentation (cards, buttons, discard animation)
/// lives on the paired RewardEncounterView component (CLAUDE.md rule 28). Only Confirm() advances it
/// — unlike LoadoutEncounter, a stray selection change alone must not grant a reward early. See
/// docs/Rewards.md and docs/Encounters.md.
/// </summary>
public class RewardEncounter : Encounter
{
    public RewardPickSO Data { get; private set; }
    public IReadOnlyList<RewardSO> DrawnRewards { get; private set; }
    public RewardSO SelectedReward { get; private set; }

    public event Action<IReadOnlyList<RewardSO>> OnRewardsDrawn;
    public event Action<RewardSO> OnSelectionChanged;
    public event Action OnClaimed;

    public override void Play(EncounterSO data)
    {
        Data = (RewardPickSO)data;
        var run = CampaignStateManager.Instance.CurrentRun;

        run.currentEnergy += Data.energyReward;
        CampaignStateManager.Instance.Save();

        DrawnRewards = DrawRewards(run);
        OnRewardsDrawn?.Invoke(DrawnRewards);
    }

    private static List<RewardSO> DrawRewards(RunState run) =>
        DebugRewards.Instance != null
            ? DebugRewards.Instance.ConsumeOverrideDraw() ?? RewardDrawer.DrawThree(G.RewardList, run)
            : RewardDrawer.DrawThree(G.RewardList, run);

    public void SelectReward(RewardSO reward)
    {
        SelectedReward = reward;
        OnSelectionChanged?.Invoke(reward);
    }

    /// <summary>Claims the selected reward into RunState and saves immediately, then either
    /// self-completes (Headless) or waits for the View to call CompletePresentation() once its
    /// discard animation finishes.</summary>
    public void Confirm()
    {
        if (SelectedReward == null) return;

        var run = CampaignStateManager.Instance.CurrentRun;
        SelectedReward.Claim(run);
        CampaignStateManager.Instance.Save();

        OnClaimed?.Invoke();
        if (Headless) CompletePresentation();
    }
}
