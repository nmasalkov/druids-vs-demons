using Game._Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

/// <summary>
/// The real (not debug-only) API for campaign navigation: knows which encounter is current and
/// how to move between them (advance/reload/complete/restart). Reads and mutates RunState through
/// CampaignStateManager (the sole owner of that data — see docs/Campaign.md) rather than owning
/// any of it itself; this owns *where you are* in the campaign, not the run's data. See
/// docs/Encounters.md. Debug-only surface (used solely by CampaignProgressTool) lives in
/// CampaignManager.Debug.cs — see CLAUDE.md rule 20.
///
/// The first cross-scene-persistent object in the codebase — everything else on the per-scene
/// Global GameObject is recreated on every scene load. Uses the standard Unity duplicate-guard
/// singleton pattern: placed once in BattleScene.unity, marks itself DontDestroyOnLoad, and any
/// later scene load's copy of the same placed GameObject self-destructs on Awake().
/// </summary>
public partial class CampaignManager : MonoBehaviour
{
    public static CampaignManager Instance { get; private set; }

    // How long the "Player wins!"/"Enemy wins!" message sits on screen before the campaign
    // transition (advance/reload/complete, or the post-victory reward pick) actually happens.
    private const float BattleResultDelaySeconds = 4f;

    [SerializeField] private EncounterListSO encounterList;

    public EncounterListSO EncounterList => encounterList;

    public FightSO CurrentFight =>
        encounterList.fights[Mathf.Clamp(CampaignStateManager.Instance.CurrentRun.currentEncounterIndex, 0, encounterList.fights.Count - 1)];

    public int CurrentEncounterIndex => CampaignStateManager.Instance.CurrentRun.currentEncounterIndex;
    public bool HasNextEncounter => CampaignStateManager.Instance.CurrentRun.currentEncounterIndex < encounterList.fights.Count - 1;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartNewRun()
    {
        CampaignStateManager.Instance.ReplaceRunState(CampaignStateManager.CreateFreshRunState());
        CampaignStateManager.Instance.Save();
        LoadCurrentEncounter();
    }

    public void ResetCurrentEncounter()
    {
        GameManager.Instance.RestartBattle();
    }

    public void AdvanceToNextEncounter()
    {
        if (!HasNextEncounter)
        {
            Debug.LogWarning("CampaignManager: already at the last encounter.");
            return;
        }

        CampaignStateManager.Instance.CurrentRun.currentEncounterIndex++;
        CampaignStateManager.Instance.Save();

        LoadCurrentEncounter();
    }

    /// <summary>
    /// Called by GameOverState once the player has won: after a delay (so the "Player wins!"
    /// message is readable), either shows the current fight's reward pick as an overlay inside
    /// BattleScene (if hasReward — BattleRewardPresenter.HandleRewardCompleted() then calls
    /// CompleteCurrentEncounter() once it's confirmed), or completes/advances immediately if there's
    /// no reward. See docs/Encounters.md.
    /// </summary>
    public void ResolveVictory()
    {
        int generation = GameManager.Instance.Generation;
        DoAfterDelay.Execute(() =>
        {
            if (GameManager.IsStale(generation)) return;

            if (CurrentFight.hasReward)
                BattleRewardPresenter.Instance.ShowReward(CurrentFight);
            else
                CompleteCurrentEncounter();
        }, BattleResultDelaySeconds);
    }

    /// <summary>
    /// Advances to the next encounter, or completes the campaign if this was the last one.
    /// Called directly by ResolveVictory() when there's no reward, or by
    /// BattleRewardPresenter once the reward pick is confirmed — so a fight that happens to be the
    /// campaign's last entry correctly completes the campaign instead of hitting
    /// AdvanceToNextEncounter()'s "already at the last encounter" no-op. See docs/Encounters.md.
    /// </summary>
    public void CompleteCurrentEncounter()
    {
        if (HasNextEncounter) AdvanceToNextEncounter();
        else CompleteCampaign();
    }

    /// <summary>
    /// Called by GameOverState on defeat (or a draw): reloads the current encounter after a
    /// delay. No RunState changes — energy/progress stay exactly where they were before this
    /// battle started. See docs/Encounters.md.
    /// </summary>
    public void ResolveDefeat()
    {
        int generation = GameManager.Instance.Generation;
        DoAfterDelay.Execute(() =>
        {
            if (GameManager.IsStale(generation)) return;
            ResetCurrentEncounter();
        }, BattleResultDelaySeconds);
    }

    /// <summary>
    /// Placeholder end-of-campaign state: no dedicated UI yet, so this just logs and freezes
    /// time — a clear "nothing more happens" signal distinct from GameOverState's silent dead
    /// end. Revisit once a real campaign-complete screen exists.
    /// </summary>
    private void CompleteCampaign()
    {
        Debug.Log("[Campaign] Congratulations! You've completed the campaign.");
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Applies the (new) current fight, scene-aware. Every real transition routes through MapScene
    /// first — MapManager decides whether the new current fight shows its loadout-pick phase in
    /// place first or hands off to BattleScene directly — so this only ever needs to either refresh
    /// MapManager in place (already in MapScene) or load MapScene (arriving from BattleScene after a
    /// fight/reward). See docs/Encounters.md.
    /// </summary>
    private void LoadCurrentEncounter()
    {
        if (SceneManager.GetActiveScene().name == SceneNames.MapScene)
            MapManager.Instance.RefreshForCurrentEncounter();
        else
            SceneManager.LoadScene(SceneNames.MapScene);
    }
}
