using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Utils;

/// <summary>
/// MapScene's orchestrator. Draws every discovered node (each with its boss icon), plays the
/// "next encounter discovered" reveal right after the progress frontier advances, then waits for
/// the player to click any discovered node — the frontier or a replay of a beaten one. A clicked
/// node plays its loadout-pick phase in place (if FightSO.hasLoadoutPick) before handing off to
/// BattleScene, or hands off directly. A frontier fight with no loadout pick auto-starts after
/// being discovered (or on boot). Driven purely by explicit calls (Start(), and
/// CampaignManager.LoadCurrentEncounter() while already in MapScene) — there is no
/// GameManager/OnBattleRestart in MapScene to subscribe to. See docs/Encounters.md.
/// </summary>
public class MapManager : MonoBehaviour
{
    public enum MapState { Revealing, AwaitingSelection, InLoadoutPick, StartingFight }

    public static MapManager Instance { get; private set; }

    [Tooltip("One entry per campaign node (non-boss-phase fight) in CampaignManager.EncounterList, " +
        "same order — AssignEncounters() maps them positionally at Start().")]
    [SerializeField] private MapEncounterPoint[] points;

    [SerializeField] private Transform encounterParent;
    [SerializeField] private LoadoutPickEncounter loadoutEncounterPrefab;

    [Tooltip("Pause after the path leading to the newly discovered point is revealed, before its enemy icon pops in.")]
    [SerializeField] private float revealDelaySeconds = 1f;
    [Tooltip("Pause after a point's arrive feedback plays, before the fight/loadout pick actually starts.")]
    [FormerlySerializedAs("reachFeedbackDuration")]
    [SerializeField] private float arriveFeedbackDuration = 0.5f;
    [Tooltip("Pause after a loadout pick completes, before the fight-arrival shake feedback plays.")]
    [SerializeField] private float postLoadoutPauseSeconds = 0.5f;

    [Header("Clickable pulse")]
    [Tooltip("How much bigger a clickable (discovered) point grows at the peak of its pulse, as a " +
             "fraction of its authored scale (0.08 = +8%).")]
    [SerializeField] private float pulseMagnitude = 0.08f;
    [Tooltip("Seconds for one half of the pulse (grow or shrink) — a full pulse is twice this.")]
    [SerializeField] private float pulseHalfPeriod = 0.6f;
    [Tooltip("Pause after a newly discovered enemy icon pops in, before every clickable point starts pulsing.")]
    [SerializeField] private float pulseStartDelay = 0.6f;

    private Encounter _activeEncounter;
    private MapEncounterPoint _selectedPoint;

    /// <summary>Read-only for MapManagerEditor's live Inspector view (CLAUDE.md rule 19).</summary>
    public IReadOnlyList<MapEncounterPoint> Points => points;
    public MapState State { get; private set; }
    public CampaignManager.MapEntry LastEntry { get; private set; }
    public MapEncounterPoint SelectedPoint => _selectedPoint;

    private static int FrontierNode => CampaignManager.Instance.ProgressNodeIndex;
    private static FightSO FrontierFight => CampaignManager.Instance.EncounterList.fights[
        CampaignManager.Instance.EncounterIndexOfNode(FrontierNode)];

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        AssignEncounters();
        foreach (var point in points) point.OnClicked += SelectPoint;
        EnterMap();
    }

    void OnDestroy()
    {
        foreach (var point in points) point.OnClicked -= SelectPoint;
    }

    /// <summary>
    /// Positionally maps every campaign *node* onto the matching serialized point — replaces each
    /// point's own per-instance fight wiring. Boss phases (FightSO.continuesPreviousFight) are
    /// skipped: a phase happens inside its node's fight and is never travelled to, so it gets no
    /// point of its own and doesn't shift the ones after it. Throws if there aren't enough placed
    /// points; this is a content-authoring/scene-setup mismatch that should surface immediately,
    /// not fail silently. See docs/Encounters.md.
    /// </summary>
    private void AssignEncounters()
    {
        var fights = CampaignManager.Instance.EncounterList.fights;
        int nodeCount = CampaignManager.Instance.NodeCount;

        if (points.Length < nodeCount)
            throw new Exception($"MapManager: only {points.Length} MapEncounterPoint(s) assigned in " +
                $"the Inspector, but EncounterList has {nodeCount} campaign node(s) — add one point " +
                $"per node. (Fights marked continuesPreviousFight are boss phases and don't get one.)");

        int node = 0;
        for (int i = 0; i < fights.Count; i++)
        {
            if (fights[i].continuesPreviousFight) continue;
            points[node].SetFight(fights[i]);
            node++;
        }
    }

    /// <summary>
    /// Presents the map for however it was entered (CampaignManager.ConsumeMapEntry()):
    /// - Reveal (frontier just advanced / new run): animate the walk to the new frontier and pop its
    ///   enemy icon, then auto-start it if it has no loadout pick, else wait for a click.
    /// - Boot (MapScene is the session's first scene): show the discovered nodes as-is, then
    ///   auto-start the frontier if it has no loadout pick, else wait for a click.
    /// - Return (Go to map, replay win): show the discovered nodes as-is and wait for a click.
    /// Called from Start() and directly by CampaignManager.LoadCurrentEncounter() when already in
    /// MapScene. No re-entrancy guard — only CampaignProgressTool's debug buttons can call in
    /// mid-sequence (YAGNI).
    /// </summary>
    public void EnterMap()
    {
        DestroyActiveEncounter();
        LastEntry = CampaignManager.Instance.ConsumeMapEntry();

        if (IsRevealEntry()) { PlayFrontierReveal(); return; }

        DrawDiscovered(FrontierNode);
        if (AutoStartsOnBoot()) { AutoStartFrontier(); return; }
        AwaitSelection();
    }

    private bool IsRevealEntry() => LastEntry == CampaignManager.MapEntry.Reveal;

    // Resuming a save on a frontier with nothing to pick (today only Fight 1) goes straight into
    // the fight, same as discovering it would. Never on Return — that would trap the player.
    private bool AutoStartsOnBoot() =>
        LastEntry == CampaignManager.MapEntry.Boot && !FrontierFight.hasLoadoutPick;

    /// <summary>
    /// Static end-state for nodes 0..frontier: past ones Complete with both paths, the frontier
    /// Current with its incoming path, every discovered enemy icon visible at full size (defeated ones alongside CompleteEncounterVisual), the rest
    /// Future with no icon. Nothing animated.
    /// </summary>
    private void DrawDiscovered(int frontier)
    {
        DrawHistory(frontier);
        var frontierPoint = points[frontier];
        frontierPoint.ShowPathIn();
        frontierPoint.SetCurrent();
        frontierPoint.ShowEnemyIconInstant();
        if (frontier > 0) points[frontier - 1].ShowPathOut();
    }

    /// <summary>
    /// Everything except the frontier itself: hide every path (clean slate — leftover path state
    /// isn't trustworthy, e.g. after a debug override jumps the index backward), stop pulsing,
    /// past points Complete with icon and paths shown, the rest Future and iconless.
    /// </summary>
    private void DrawHistory(int frontier)
    {
        State = MapState.Revealing;
        int nodeCount = CampaignManager.Instance.NodeCount;

        foreach (var point in points)
        {
            point.HidePaths();
            point.StopPulse();
            point.HideEnemyIcon();
        }

        for (int i = 0; i < nodeCount; i++)
        {
            if (i < frontier) points[i].SetComplete();
            else points[i].SetFuture();
        }

        // Defeated nodes show BOTH CompleteEncounterVisual and their enemy icon (they pulse together
        // with the root once the map awaits a selection).
        for (int i = 0; i < frontier; i++)
        {
            points[i].ShowEnemyIconInstant();
            points[i].ShowPathIn();
            if (i < frontier - 1) points[i].ShowPathOut();
        }
    }

    /// <summary>
    /// The "next encounter discovered" beat: nothing pulses, the path from the previous point
    /// draws in dot by dot across revealDelaySeconds, then the frontier flips to Current and its
    /// enemy icon pops in.
    /// </summary>
    private void PlayFrontierReveal()
    {
        int frontier = FrontierNode;
        DrawHistory(frontier);
        points[frontier].ShowPathIn();

        if (frontier > 0) points[frontier - 1].RevealPathOutGradually(revealDelaySeconds);

        DoAfterDelay.Execute(DiscoverFrontier, revealDelaySeconds);
    }

    private void DiscoverFrontier()
    {
        var frontierPoint = points[FrontierNode];
        frontierPoint.SetCurrent();
        frontierPoint.RevealEnemyIcon();

        if (!FrontierFight.hasLoadoutPick) { AutoStartFrontier(); return; }

        frontierPoint.PlayArriveRegularFeedback();
        DoAfterDelay.Execute(AwaitSelection, pulseStartDelay);
    }

    /// <summary>The "reached encounter" state: every discovered point pulses and is clickable.</summary>
    private void AwaitSelection()
    {
        State = MapState.AwaitingSelection;
        for (int i = 0; i <= FrontierNode; i++)
            points[i].StartPulse(pulseMagnitude, pulseHalfPeriod);
    }

    private void AutoStartFrontier()
    {
        CampaignManager.Instance.SelectNode(FrontierNode);
        _selectedPoint = points[FrontierNode];
        StartFightFromPoint();
    }

    /// <summary>Click handler for every point — ignored unless waiting for a choice and the point is discovered.</summary>
    private void SelectPoint(MapEncounterPoint point)
    {
        if (!IsSelectable(point)) return;

        StopAllPulses();
        _selectedPoint = point;
        CampaignManager.Instance.SelectNode(Array.IndexOf(points, point));

        if (point.Fight.hasLoadoutPick) OpenLoadoutPick();
        else StartFightFromPoint();
    }

    private bool IsSelectable(MapEncounterPoint point) =>
        State == MapState.AwaitingSelection && Array.IndexOf(points, point) <= FrontierNode;

    private void StopAllPulses()
    {
        foreach (var point in points) point.StopPulse();
    }

    private void OpenLoadoutPick()
    {
        State = MapState.InLoadoutPick;
        _selectedPoint.PlayArriveRegularFeedback();
        DoAfterDelay.Execute(StartLoadoutPick, arriveFeedbackDuration);
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
        DoAfterDelay.Execute(StartFightFromPoint, postLoadoutPauseSeconds);
    }

    private void StartFightFromPoint()
    {
        State = MapState.StartingFight;
        _selectedPoint.PlayArriveFightFeedback();
        DoAfterDelay.Execute(StartFight, arriveFeedbackDuration);
    }

    private void StartFight() => SceneManager.LoadScene(SceneNames.BattleScene);

    /// <summary>
    /// Instant-resolve counterpart of clicking a node (CLAUDE.md rule 7) — for a future automated
    /// test harness, not called anywhere in the normal flow yet, matching how
    /// BattleState.ResolveBattleInstant()/NukeState.ResolveNukesInstant() etc. currently have no
    /// caller either. Selects the node with zero animation, headlessly confirms its loadout pick
    /// unchanged (if any) via a bare, view-less LoadoutPickEncounter instance, then loads
    /// BattleScene immediately.
    /// </summary>
    public void ResolvePointInstant(int nodeIndex)
    {
        DestroyActiveEncounter();
        CampaignManager.Instance.SelectNode(nodeIndex);

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
