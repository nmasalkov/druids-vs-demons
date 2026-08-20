using Game._Scripts.Global;
using UnityEngine;

/// <summary>
/// BattleScene-local dispatcher for the post-victory reward pick: instantiates RewardEncounter.prefab
/// as an overlay directly inside BattleScene (on the game-over screen, round loop already halted),
/// waits for its OnCompleted, then hands off to CampaignManager.CompleteCurrentEncounter(). Replaces
/// EncounterPlayer, whose whole reason for existing (dispatching a non-fight EncounterSO inside
/// BattleScene) no longer applies now that every campaign-list entry is a FightSO. See
/// docs/Encounters.md, docs/Rewards.md.
///
/// Scene-local singleton (recreated on every BattleScene load, NOT DontDestroyOnLoad) — same pattern
/// as RollStateManager, unlike the cross-scene-persistent CampaignManager/CampaignStateManager.
/// </summary>
public class BattleRewardPresenter : MonoBehaviour
{
    public static BattleRewardPresenter Instance { get; private set; }

    [SerializeField] private RewardEncounter rewardEncounterPrefab;
    [SerializeField] private Transform encounterParent;

    private RewardEncounter _activeReward;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // A pause-menu restart can fire while the reward overlay is still up (GameOverState halts the
        // round loop but doesn't disable PauseMenuController) — clean up the stale overlay so a
        // restarted battle doesn't have a leftover reward pick floating over it. CLAUDE.md rule 16.
        GameManager.OnBattleRestart += DestroyActiveReward;
    }

    void OnDestroy()
    {
        GameManager.OnBattleRestart -= DestroyActiveReward;
    }

    public void ShowReward(FightSO fight)
    {
        DestroyActiveReward();
        _activeReward = Instantiate(rewardEncounterPrefab, encounterParent);
        _activeReward.OnCompleted += HandleRewardCompleted;
        _activeReward.Play(fight.rewardAmount);
    }

    /// <summary>Zero-delay counterpart of ShowReward (CLAUDE.md rule 7) — bare backend, no prefab/UI,
    /// auto-selects the first drawn reward and confirms immediately. Confirm() self-completing under
    /// Headless naturally chains into HandleRewardCompleted the same way the real UI path does.</summary>
    public void ShowRewardInstant(FightSO fight)
    {
        DestroyActiveReward();
        _activeReward = new GameObject(nameof(RewardEncounter) + " (Instant)").AddComponent<RewardEncounter>();
        _activeReward.Headless = true;
        _activeReward.OnCompleted += HandleRewardCompleted;
        _activeReward.Play(fight.rewardAmount);
        _activeReward.SelectReward(_activeReward.DrawnRewards[0]);
        _activeReward.Confirm();
    }

    private void HandleRewardCompleted()
    {
        DestroyActiveReward();
        CampaignManager.Instance.CompleteCurrentEncounter();
    }

    private void DestroyActiveReward()
    {
        if (_activeReward == null) return;
        _activeReward.OnCompleted -= HandleRewardCompleted;
        Destroy(_activeReward.gameObject);
        _activeReward = null;
    }
}
