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
    // transition (advance/reload/complete) actually happens.
    private const float BattleResultDelaySeconds = 4f;

    [SerializeField] private EncounterListSO encounterList;

    public EncounterListSO EncounterList => encounterList;

    public EncounterSO CurrentEncounter =>
        encounterList.encounters[Mathf.Clamp(CampaignStateManager.Instance.CurrentRun.currentEncounterIndex, 0, encounterList.encounters.Count - 1)];

    public int CurrentEncounterIndex => CampaignStateManager.Instance.CurrentRun.currentEncounterIndex;
    public bool HasNextEncounter => CampaignStateManager.Instance.CurrentRun.currentEncounterIndex < encounterList.encounters.Count - 1;

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
        CampaignStateManager.Instance.ReplaceRunState(new RunState());
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
    /// message is readable), either advances to the next encounter or, if this was the last one,
    /// completes the campaign. No RunState change happens here anymore — energy rewards are now
    /// granted explicitly by a RewardPickSO encounter, not automatically on victory. See
    /// docs/Encounters.md.
    /// </summary>
    public void ResolveVictory()
    {
        int generation = GameManager.Instance.Generation;
        DoAfterDelay.Execute(() =>
        {
            if (GameManager.IsStale(generation)) return;
            CompleteCurrentEncounter();
        }, BattleResultDelaySeconds);
    }

    /// <summary>
    /// Advances to the next encounter, or completes the campaign if this was the last one.
    /// Shared by ResolveVictory() (after its delay) and EncounterPlayer's pick-screen completion
    /// (immediately), so a LoadoutPickSO/RewardPickSO that happens to be the campaign's last entry
    /// correctly completes the campaign instead of hitting AdvanceToNextEncounter()'s "already at
    /// the last encounter" no-op. See docs/Encounters.md.
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
    /// Applies the (new) current encounter, scene-aware. Under the debug
    /// ProcessAllEncountersInBattleScene flag, always soft-reloads BattleScene in place exactly like
    /// before MapScene existed (the headless/automation-friendly path — CLAUDE.md rule 7). Otherwise,
    /// every real transition routes through MapScene first — MapManager decides whether the new
    /// current encounter is played in place (a pick screen) or hands off to BattleScene (a fight) —
    /// so this only ever needs to either refresh MapManager in place (already in MapScene) or load
    /// MapScene (arriving from BattleScene after a fight). See docs/Encounters.md.
    /// </summary>
    private void LoadCurrentEncounter()
    {
        if (ProcessAllEncountersInBattleScene)
        {
            CampaignStateManager.Instance.ApplyEncounterToScene();
            GameManager.Instance.RestartBattle();
            return;
        }

        if (SceneManager.GetActiveScene().name == SceneNames.MapScene)
            MapManager.Instance.RefreshForCurrentEncounter();
        else
            SceneManager.LoadScene(SceneNames.MapScene);
    }
}
