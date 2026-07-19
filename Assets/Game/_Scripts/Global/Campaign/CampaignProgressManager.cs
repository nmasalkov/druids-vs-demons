using Game._Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The real (not debug-only) API for campaign navigation: knows which encounter is current and
/// how to move between them. CampaignManager stays owner of RunState and applying it to the
/// current scene; this owns *where you are* in the campaign and the three ways to change that.
/// See docs/Encounters.md. Debug-only surface (used solely by CampaignProgressTool) lives in
/// CampaignProgressManager.Debug.cs — see CLAUDE.md rule 20.
///
/// The first cross-scene-persistent object in the codebase — everything else on the per-scene
/// Global GameObject is recreated on every scene load. Uses the standard Unity duplicate-guard
/// singleton pattern: placed once in BattleScene.unity, marks itself DontDestroyOnLoad, and any
/// later scene load's copy of the same placed GameObject self-destructs on Awake().
/// </summary>
public partial class CampaignProgressManager : MonoBehaviour
{
    public static CampaignProgressManager Instance { get; private set; }

    [SerializeField] private EncounterListSO encounterList;

    private int _currentEncounterIndex;

    public EncounterSO CurrentEncounter =>
        encounterList.encounters[Mathf.Clamp(_currentEncounterIndex, 0, encounterList.encounters.Count - 1)];

    public int CurrentEncounterIndex => _currentEncounterIndex;
    public bool HasNextEncounter => _currentEncounterIndex < encounterList.encounters.Count - 1;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Self-contained read, same as CampaignManager.Awake() — can't rely on
        // CampaignManager.Instance existing yet (Awake-vs-Awake ordering is unspecified, and a
        // future map scene may not have a CampaignManager loaded at all).
        _currentEncounterIndex = CampaignManager.LoadOrCreateRunState().currentEncounterIndex;
        _overrideWindowOpen = true;
    }

    void Start()
    {
        // Closes the override window (see CampaignProgressManager.Debug.cs). Since this object is
        // DontDestroyOnLoad, Start() only ever runs once per session (a scene reload's fresh copy
        // of the placed GameObject destroys itself in Awake() before ever reaching Start()) —
        // everything after this point is real navigation, never a debug override reapplying itself.
        _overrideWindowOpen = false;
    }

    public void StartNewRun()
    {
        _currentEncounterIndex = 0;
        CampaignManager.Instance.ResetRun();
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
            Debug.LogWarning("CampaignProgressManager: already at the last encounter.");
            return;
        }

        _currentEncounterIndex++;
        CampaignManager.Instance.CurrentRun.currentEncounterIndex = _currentEncounterIndex;
        CampaignManager.Instance.Save();

        LoadCurrentEncounter();
    }

    /// <summary>
    /// If BattleScene is already loaded, applies the (new) current encounter and does an in-place
    /// battle restart instead of a full scene reload — SceneManager.LoadScene collapses the whole
    /// scene hierarchy, which is disruptive when we're already sitting in the scene we'd just
    /// reload. Only actually loads the scene when arriving from somewhere else (e.g. a future
    /// MapScene). See docs/Encounters.md.
    /// </summary>
    private void LoadCurrentEncounter()
    {
        if (SceneManager.GetActiveScene().name == SceneNames.BattleScene)
        {
            CampaignManager.Instance.ApplyEncounterToScene();
            GameManager.Instance.RestartBattle();
        }
        else
        {
            SceneManager.LoadScene(SceneNames.BattleScene);
        }
    }
}
