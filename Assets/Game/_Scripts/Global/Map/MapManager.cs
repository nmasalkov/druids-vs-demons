using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Utils;

/// <summary>
/// MapScene's orchestrator: reveals completed/current encounter points and either plays the current
/// pick screen in place (LoadoutPickSO/RewardPickSO) or hands off to BattleScene (FightSO), after a
/// gradual, step-by-step reveal. Driven purely by explicit calls (Start(), and
/// CampaignManager.LoadCurrentEncounter() while already in MapScene) — there is no
/// GameManager/OnBattleRestart in MapScene to subscribe to. See docs/Encounters.md.
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Tooltip("One entry per encounter in CampaignManager.EncounterList, same order — " +
        "AssignEncounters() maps them positionally at Start().")]
    [SerializeField] private MapEncounterPoint[] points;

    [SerializeField] private Transform encounterParent;

    [Tooltip("Pause after the path leading to the current point is revealed, before it flips to Current.")]
    [SerializeField] private float revealDelaySeconds = 1f;
    [Tooltip("Pause after the current point's arrive feedback plays, before the encounter actually starts.")]
    [FormerlySerializedAs("reachFeedbackDuration")]
    [SerializeField] private float arriveFeedbackDuration = 0.5f;

    private Encounter _activeEncounter;

    /// <summary>Read-only for MapManagerEditor's live Inspector view (CLAUDE.md rule 19).</summary>
    public IReadOnlyList<MapEncounterPoint> Points => points;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        AssignEncounters();
        RefreshForCurrentEncounter();
    }

    /// <summary>
    /// Positionally maps every EncounterListSO entry onto the matching serialized point
    /// (points[i] represents encounters[i]) — replaces each point's own per-instance encounterSO
    /// wiring. Throws if there aren't enough placed points for the encounter list; this is a
    /// content-authoring/scene-setup mismatch that should surface immediately, not fail silently.
    /// </summary>
    private void AssignEncounters()
    {
        var encounters = CampaignManager.Instance.EncounterList.encounters;
        if (points.Length < encounters.Count)
            throw new Exception($"MapManager: only {points.Length} MapEncounterPoint(s) assigned in " +
                $"the Inspector, but EncounterList has {encounters.Count} encounters — add one point " +
                "per encounter.");

        for (int i = 0; i < encounters.Count; i++)
            points[i].SetEncounter(encounters[i]);
    }

    /// <summary>
    /// Re-derives the map's visual state from CampaignManager.CurrentEncounter as a gradual,
    /// step-by-step reveal (every step visible, nothing jumps straight to its end state):
    /// 1. Hide every point's paths (clean slate — a point's path state from a previous refresh isn't
    ///    trustworthy, e.g. after a debug override jumps the index backward).
    /// 2. Every already-passed point (index &lt; current) snaps to Complete with both paths shown —
    ///    old history, nothing new to animate about it.
    /// 3. The current point's incoming path is revealed, then a short pause
    ///    (revealDelaySeconds) — this is "the player has walked up to it."
    /// 4. The current point flips Future -&gt; Current, playing a shake (fight) or yoyo/punch (pick
    ///    screen) via RevealCurrentPoint(), then another short pause (arriveFeedbackDuration).
    /// 5. StartCurrentEncounter() actually starts it — hands off to BattleScene for a fight, or
    ///    instantiates/plays the pick screen prefab in place.
    ///
    /// Called once from Start() and again directly by CampaignManager.LoadCurrentEncounter()
    /// whenever a transition lands on a new current encounter while already in MapScene (e.g.
    /// completing a pick screen).
    ///
    /// No re-entrancy guard: normal gameplay only ever calls this again after the previous call's
    /// full sequence (including any pick-screen completion) has resolved, so calls never overlap.
    /// CampaignProgressTool's Play-mode debug buttons are the one exception that could call in
    /// mid-sequence — debug-only exposure, left unguarded per YAGNI.
    /// </summary>
    public void RefreshForCurrentEncounter()
    {
        DestroyActiveEncounter();

        int currentIndex = CampaignManager.Instance.CurrentEncounterIndex;
        var encounters = CampaignManager.Instance.EncounterList.encounters;

        foreach (var point in points) point.HidePaths();

        for (int i = 0; i < encounters.Count; i++)
        {
            if (i < currentIndex) points[i].SetComplete();
            else points[i].SetFuture();
        }

        // Everything older than the point immediately before current is already-walked history from
        // an earlier refresh — both its paths are shown at once, no animation.
        for (int i = 0; i < currentIndex - 1; i++)
        {
            points[i].ShowPathIn();
            points[i].ShowPathOut();
        }

        var currentPoint = points[currentIndex];
        currentPoint.ShowPathIn();

        // The point immediately before current is the one transition that's actually new this call —
        // its pathOut gets the gradual dot-by-dot reveal (imitates the hero steadily walking it),
        // paced to finish right as revealDelaySeconds' wait below ends.
        if (currentIndex > 0)
        {
            var previousPoint = points[currentIndex - 1];
            previousPoint.ShowPathIn();
            previousPoint.RevealPathOutGradually(revealDelaySeconds);
        }

        DoAfterDelay.Execute(() => RevealCurrentPoint(currentPoint), revealDelaySeconds);
    }

    private void RevealCurrentPoint(MapEncounterPoint currentPoint)
    {
        currentPoint.SetCurrent();

        if (CampaignManager.Instance.CurrentEncounter is FightSO)
            currentPoint.PlayArriveFightFeedback();
        else
            currentPoint.PlayArriveRegularFeedback();

        DoAfterDelay.Execute(StartCurrentEncounter, arriveFeedbackDuration);
    }

    private void StartCurrentEncounter()
    {
        var currentEncounter = CampaignManager.Instance.CurrentEncounter;

        if (currentEncounter is FightSO)
        {
            SceneManager.LoadScene(SceneNames.BattleScene);
            return;
        }

        // Unassigned EncounterListSO slot — content-authoring state, not a wiring bug (mirrors
        // EncounterPlayer's tolerance for the same case).
        if (currentEncounter.EncounterPrefab == null) return;

        _activeEncounter = Instantiate(currentEncounter.EncounterPrefab, encounterParent);
        _activeEncounter.OnCompleted += HandleEncounterCompleted;
        _activeEncounter.Play(currentEncounter);
    }

    private void HandleEncounterCompleted()
    {
        DestroyActiveEncounter();
        CampaignManager.Instance.CompleteCurrentEncounter();
    }

    private void DestroyActiveEncounter()
    {
        if (_activeEncounter == null) return;
        _activeEncounter.OnCompleted -= HandleEncounterCompleted;
        Destroy(_activeEncounter.gameObject);
        _activeEncounter = null;
    }
}
