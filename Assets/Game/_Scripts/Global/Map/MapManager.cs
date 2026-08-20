using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Utils;

/// <summary>
/// MapScene's orchestrator: reveals completed/current fight points, then either shows the current
/// fight's loadout-pick phase in place (if FightSO.hasLoadoutPick) before handing off to BattleScene,
/// or hands off directly — after a gradual, step-by-step reveal. Driven purely by explicit calls
/// (Start(), and CampaignManager.LoadCurrentEncounter() while already in MapScene) — there is no
/// GameManager/OnBattleRestart in MapScene to subscribe to. See docs/Encounters.md.
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Tooltip("One entry per fight in CampaignManager.EncounterList, same order — " +
        "AssignEncounters() maps them positionally at Start().")]
    [SerializeField] private MapEncounterPoint[] points;

    [SerializeField] private Transform encounterParent;
    [SerializeField] private LoadoutPickEncounter loadoutEncounterPrefab;

    [Tooltip("Pause after the path leading to the current point is revealed, before it flips to Current.")]
    [SerializeField] private float revealDelaySeconds = 1f;
    [Tooltip("Pause after the current point's arrive feedback plays, before the fight/loadout pick actually starts.")]
    [FormerlySerializedAs("reachFeedbackDuration")]
    [SerializeField] private float arriveFeedbackDuration = 0.5f;
    [Tooltip("Pause after a loadout pick completes, before the fight-arrival shake feedback plays.")]
    [SerializeField] private float postLoadoutPauseSeconds = 0.5f;

    private Encounter _activeEncounter;
    private MapEncounterPoint _currentPoint;

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
    /// (points[i] represents fights[i]) — replaces each point's own per-instance fight wiring.
    /// Throws if there aren't enough placed points for the encounter list; this is a
    /// content-authoring/scene-setup mismatch that should surface immediately, not fail silently.
    /// </summary>
    private void AssignEncounters()
    {
        var fights = CampaignManager.Instance.EncounterList.fights;
        if (points.Length < fights.Count)
            throw new Exception($"MapManager: only {points.Length} MapEncounterPoint(s) assigned in " +
                $"the Inspector, but EncounterList has {fights.Count} fights — add one point per fight.");

        for (int i = 0; i < fights.Count; i++)
            points[i].SetFight(fights[i]);
    }

    /// <summary>
    /// Re-derives the map's visual state from CampaignManager.CurrentFight as a gradual,
    /// step-by-step reveal (every step visible, nothing jumps straight to its end state):
    /// 1. Hide every point's paths (clean slate — a point's path state from a previous refresh isn't
    ///    trustworthy, e.g. after a debug override jumps the index backward).
    /// 2. Every already-passed point (index &lt; current) snaps to Complete with both paths shown —
    ///    old history, nothing new to animate about it.
    /// 3. The current point's incoming path is revealed, then a short pause
    ///    (revealDelaySeconds) — this is "the player has walked up to it."
    /// 4. The current point flips Future -&gt; Current via RevealCurrentPoint(). If the fight has no
    ///    loadout-pick phase, this plays the fight-arrival shake directly and, after a pause
    ///    (arriveFeedbackDuration), loads BattleScene. If it does, this plays the plain arrival
    ///    yoyo/punch instead and, after the same pause, shows the loadout pick in place — only once
    ///    that's confirmed (and a further postLoadoutPauseSeconds pause) does the fight-arrival shake
    ///    play and BattleScene load.
    ///
    /// Called once from Start() and again directly by CampaignManager.LoadCurrentEncounter()
    /// whenever a transition lands on a new current fight while already in MapScene (e.g. after a
    /// reward pick completes in BattleScene and the campaign advances). Never re-entered mid-node —
    /// completing a loadout pick continues into the same node's fight, it doesn't loop back here.
    ///
    /// No re-entrancy guard: normal gameplay only ever calls this again after the previous call's
    /// full sequence has resolved, so calls never overlap. CampaignProgressTool's Play-mode debug
    /// buttons are the one exception that could call in mid-sequence — debug-only exposure, left
    /// unguarded per YAGNI.
    /// </summary>
    public void RefreshForCurrentEncounter()
    {
        DestroyActiveEncounter();

        int currentIndex = CampaignManager.Instance.CurrentEncounterIndex;
        var fights = CampaignManager.Instance.EncounterList.fights;

        foreach (var point in points) point.HidePaths();

        for (int i = 0; i < fights.Count; i++)
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

        _currentPoint = points[currentIndex];
        _currentPoint.ShowPathIn();

        // The point immediately before current is the one transition that's actually new this call —
        // its pathOut gets the gradual dot-by-dot reveal (imitates the hero steadily walking it),
        // paced to finish right as revealDelaySeconds' wait below ends.
        if (currentIndex > 0)
        {
            var previousPoint = points[currentIndex - 1];
            previousPoint.ShowPathIn();
            previousPoint.RevealPathOutGradually(revealDelaySeconds);
        }

        DoAfterDelay.Execute(RevealCurrentPoint, revealDelaySeconds);
    }

    private void RevealCurrentPoint()
    {
        _currentPoint.SetCurrent();

        if (CampaignManager.Instance.CurrentFight.hasLoadoutPick)
        {
            _currentPoint.PlayArriveRegularFeedback();
            DoAfterDelay.Execute(StartLoadoutPick, arriveFeedbackDuration);
        }
        else
        {
            _currentPoint.PlayArriveFightFeedback();
            DoAfterDelay.Execute(StartFight, arriveFeedbackDuration);
        }
    }

    private void StartLoadoutPick()
    {
        var loadout = Instantiate(loadoutEncounterPrefab, encounterParent);
        _activeEncounter = loadout;
        _activeEncounter.OnCompleted += HandleLoadoutPickCompleted;
        loadout.Play();
    }

    private void HandleLoadoutPickCompleted()
    {
        DestroyActiveEncounter();
        DoAfterDelay.Execute(ArriveAtFightAfterLoadout, postLoadoutPauseSeconds);
    }

    private void ArriveAtFightAfterLoadout()
    {
        _currentPoint.PlayArriveFightFeedback();
        DoAfterDelay.Execute(StartFight, arriveFeedbackDuration);
    }

    private void StartFight() => SceneManager.LoadScene(SceneNames.BattleScene);

    /// <summary>
    /// Instant-resolve counterpart of the current node's full sequence (CLAUDE.md rule 7) — for a
    /// future automated test harness, not called anywhere in the normal flow yet, matching how
    /// BattleState.ResolveBattleInstant()/NukeState.ResolveNukesInstant() etc. currently have no
    /// caller either. Marks the point current with zero animation, headlessly confirms the loadout
    /// pick unchanged (if any) via a bare, view-less LoadoutPickEncounter instance, then loads
    /// BattleScene immediately.
    /// </summary>
    public void ResolveCurrentPointInstant()
    {
        DestroyActiveEncounter();

        int currentIndex = CampaignManager.Instance.CurrentEncounterIndex;
        points[currentIndex].SetCurrent();

        if (CampaignManager.Instance.CurrentFight.hasLoadoutPick)
        {
            var loadout = new GameObject(nameof(LoadoutPickEncounter) + " (Instant)").AddComponent<LoadoutPickEncounter>();
            loadout.Headless = true;
            loadout.Play();
            loadout.Confirm();
            Destroy(loadout.gameObject);
        }

        SceneManager.LoadScene(SceneNames.BattleScene);
    }

    private void DestroyActiveEncounter()
    {
        if (_activeEncounter == null) return;
        _activeEncounter.OnCompleted -= HandleLoadoutPickCompleted;
        Destroy(_activeEncounter.gameObject);
        _activeEncounter = null;
    }
}
