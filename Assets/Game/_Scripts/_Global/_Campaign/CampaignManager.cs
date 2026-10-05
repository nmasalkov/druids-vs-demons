using System;
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

    /// <summary>
    /// How MapScene should present itself the next time MapManager enters it — consumed (and reset
    /// to Return) by <see cref="ConsumeMapEntry"/>. Boot: MapScene is the session's first scene
    /// (resume a save straight at "pick who to fight"). Reveal: the frontier just advanced (or a
    /// new run started) — play the discovery animation. Return: back from a battle without
    /// progressing (Go to map, or a replay win). See docs/Encounters.md.
    /// </summary>
    public enum MapEntry { Boot, Reveal, Return }

    /// <summary>
    /// Fired once a defeat (or draw) has been on screen for BattleResultDelaySeconds. Replaces the
    /// old automatic restart — PauseMenuController subscribes and shows the pause panel locked
    /// (no unpause), so the player picks Restart or Go to map. See docs/Encounters.md.
    /// </summary>
    public static event Action OnDefeatResolved;

    [SerializeField] private EncounterListSO encounterList;

    // Session-only: the encounter the player picked on the map (a replay of a beaten node, or the
    // frontier), or -1 to fall back to the saved frontier. Never persisted — a reload always
    // resumes at the frontier. Also carries boss-phase advances, so a phase never touches RunState.
    private int _selectedEncounterIndex = -1;

    private MapEntry _pendingMapEntry = MapEntry.Boot;

    public EncounterListSO EncounterList => encounterList;

    public FightSO CurrentFight => encounterList.fights[ClampIndex(CurrentEncounterIndex)];

    /// <summary>
    /// The encounter actually being played: the map selection if one was made this session, else
    /// the saved progress frontier. One value with a fallback, not a synced copy (rule 22).
    /// </summary>
    public int CurrentEncounterIndex => _selectedEncounterIndex >= 0 ? _selectedEncounterIndex : ProgressEncounterIndex;

    /// <summary>The saved progress frontier — the furthest node reached (RunState.currentEncounterIndex).</summary>
    public int ProgressEncounterIndex => CampaignStateManager.Instance.CurrentRun.currentEncounterIndex;

    /// <summary>Map node of the progress frontier — every node up to and including it is discovered/clickable.</summary>
    public int ProgressNodeIndex => NodeIndexOf(ProgressEncounterIndex);

    /// <summary>True while replaying a node the player has already beaten (behind the frontier).</summary>
    public bool IsReplay => NodeIndexOf(CurrentEncounterIndex) < ProgressNodeIndex;

    /// <summary>Read-only peek at the pending map entry, for CampaignManagerEditor (rule 19).</summary>
    public MapEntry PendingMapEntry => _pendingMapEntry;

    /// <summary>
    /// Whether another campaign *node* follows the current one. Boss phases
    /// (<see cref="FightSO.continuesPreviousFight"/>) don't count — they belong to the node they
    /// continue — so the campaign correctly ends after the final boss's last phase instead of
    /// treating that phase as "one more encounter". See docs/Encounters.md.
    /// </summary>
    public bool HasNextEncounter => NextNodeStartIndex() != -1;

    /// <summary>
    /// Number of campaign nodes (fights that aren't boss phases) — i.e. how many
    /// MapEncounterPoints MapScene has to provide. See docs/Encounters.md.
    /// </summary>
    public int NodeCount
    {
        get
        {
            int count = 0;
            foreach (var fight in encounterList.fights)
                if (!fight.continuesPreviousFight) count++;
            return count;
        }
    }

    /// <summary>
    /// Index of the fight that *opens* the node owning <paramref name="encounterIndex"/> — walks
    /// back over any boss phases. Identity for an ordinary fight. This is what a restart or defeat
    /// rewinds to, so a multi-phase boss always replays from its real first phase.
    /// </summary>
    public int NodeStartIndex(int encounterIndex)
    {
        int index = ClampIndex(encounterIndex);
        while (index > 0 && encounterList.fights[index].continuesPreviousFight) index--;
        return index;
    }

    /// <summary>
    /// Map-point index for an encounter index: counts only nodes, and resolves a boss phase to the
    /// node it continues (a phase is never its own map destination). See MapManager.
    /// </summary>
    public int NodeIndexOf(int encounterIndex)
    {
        int start = NodeStartIndex(encounterIndex);
        int node = 0;
        for (int i = 0; i < start; i++)
            if (!encounterList.fights[i].continuesPreviousFight) node++;
        return node;
    }

    /// <summary>
    /// Inverse of <see cref="NodeIndexOf"/>: the encounter index that opens map node
    /// <paramref name="nodeIndex"/>.
    /// </summary>
    public int EncounterIndexOfNode(int nodeIndex)
    {
        int node = 0;
        for (int i = 0; i < encounterList.fights.Count; i++)
        {
            if (encounterList.fights[i].continuesPreviousFight) continue;
            if (node == nodeIndex) return i;
            node++;
        }
        throw new ArgumentOutOfRangeException(nameof(nodeIndex), $"CampaignManager: no campaign node {nodeIndex}.");
    }

    /// <summary>
    /// The current fight's next phase, or null if it's a one-phase fight. Derived purely from list
    /// order — the entry right after this one, if it's flagged as a continuation — so there's a
    /// single source of truth for "what comes next" (rule 22) rather than a separate per-fight
    /// reference that could disagree with the list.
    /// </summary>
    public FightSO NextPhase
    {
        get
        {
            int next = CurrentEncounterIndex + 1;
            if (next >= encounterList.fights.Count) return null;
            return encounterList.fights[next].continuesPreviousFight ? encounterList.fights[next] : null;
        }
    }

    /// <summary>Index of the next campaign node after the progress frontier, or -1 if there is none.</summary>
    private int NextNodeStartIndex()
    {
        for (int i = ProgressEncounterIndex + 1; i < encounterList.fights.Count; i++)
            if (!encounterList.fights[i].continuesPreviousFight) return i;
        return -1;
    }

    /// <summary>
    /// Clamps an encounter index into range, warning loudly when it was actually out of bounds.
    /// A silent clamp here used to swallow a debug profile authored with an index past the end of
    /// the campaign — it just played the last fight instead, with nothing in the console to say so.
    /// </summary>
    private int ClampIndex(int index)
    {
        if (index >= 0 && index < encounterList.fights.Count) return index;

        Debug.LogWarning($"CampaignManager: currentEncounterIndex {index} is outside the campaign " +
                         $"(valid 0..{encounterList.fights.Count - 1}) — clamping. Check the debug profile or save.");
        return Mathf.Clamp(index, 0, encounterList.fights.Count - 1);
    }

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
        ClearSelection();
        _pendingMapEntry = MapEntry.Reveal;
        LoadCurrentEncounter();
    }

    /// <summary>
    /// Called by MapManager when the player clicks a discovered map point: makes that node's
    /// opening fight the one BattleScene plays. Session-only — never saved.
    /// </summary>
    public void SelectNode(int nodeIndex) => _selectedEncounterIndex = EncounterIndexOfNode(nodeIndex);

    /// <summary>Returns how MapScene should present itself this time, and resets it to Return.</summary>
    public MapEntry ConsumeMapEntry()
    {
        var entry = _pendingMapEntry;
        _pendingMapEntry = MapEntry.Return;
        return entry;
    }

    private void ClearSelection() => _selectedEncounterIndex = -1;

    /// <summary>
    /// Restarts the fight in place. Rewinds the encounter index off any boss phase and reverts the
    /// enemy side to this node's own opening fight first — both no-ops for an ordinary encounter,
    /// but essential after a boss phase transition: without them a restart or defeat reached in
    /// phase 2 would replay phase 2 instead of the fight's real opening. Order matters — the index
    /// has to move back before RevertToEncounterFight() reads CurrentFight off it, or the revert
    /// would compare phase 2 against itself and no-op.
    /// </summary>
    public void ResetCurrentEncounter()
    {
        RewindToNodeStart();
        CampaignStateManager.Instance.RevertToEncounterFight();
        GameManager.Instance.RestartBattle();
    }

    /// <summary>
    /// Advances onto the current fight's next phase, in place: bumps the encounter index but
    /// deliberately neither saves nor loads a scene. Called only by BossPhaseTransitionState, which
    /// swaps the enemy side over itself and drops straight back into the round loop.
    ///
    /// Not saving is the point — a boss's phases have to be won in a single session, so a defeat in
    /// phase 2 (or quitting there) resumes at the fight's first phase rather than banking progress
    /// halfway through. ResetCurrentEncounter()'s rewind is the in-memory half of the same rule.
    /// </summary>
    public void AdvanceToBossPhase()
    {
        _selectedEncounterIndex = CurrentEncounterIndex + 1;
    }

    /// <summary>
    /// Puts the encounter index back on the fight that opens the current node, undoing any boss
    /// phase advance. Not saved, because the advance wasn't either.
    /// </summary>
    private void RewindToNodeStart()
    {
        _selectedEncounterIndex = NodeStartIndex(CurrentEncounterIndex);
    }

    /// <summary>
    /// Advances to the next campaign node, skipping over any boss phases in between — those are
    /// only ever reached mid-fight through AdvanceToBossPhase(), never navigated to.
    /// </summary>
    public void AdvanceToNextEncounter()
    {
        int next = NextNodeStartIndex();
        if (next == -1)
        {
            Debug.LogWarning("CampaignManager: already at the last encounter.");
            return;
        }

        CampaignStateManager.Instance.CurrentRun.currentEncounterIndex = next;
        CampaignStateManager.Instance.Save();

        ClearSelection();
        _pendingMapEntry = MapEntry.Reveal;
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
        if (IsReplay) { ReturnToMap(); return; }
        if (HasNextEncounter) AdvanceToNextEncounter();
        else CompleteCampaign();
    }

    /// <summary>
    /// Leaves BattleScene for the map without progressing: clears the map selection (so the
    /// current encounter falls back to the frontier) and has MapManager land straight in its
    /// "pick who to fight" state. Used after a replay win (rewards already claimed) and by
    /// AbandonEncounter(). See docs/Encounters.md.
    /// </summary>
    public void ReturnToMap()
    {
        ClearSelection();
        _pendingMapEntry = MapEntry.Return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.MapScene);
    }

    /// <summary>
    /// Pause menu's "Go to map": quits the fight mid-way. Refunds this attempt's reroll spend
    /// exactly like a restart would, then returns to the map. Kept separate from ReturnToMap()
    /// because the replay-win path must keep the reward energy it just granted.
    /// </summary>
    public void AbandonEncounter()
    {
        EnergyController.Instance.RevertToEncounterStart();
        ReturnToMap();
    }

    /// <summary>
    /// Called by GameOverState on defeat (or a draw): after a delay, fires OnDefeatResolved so the
    /// pause menu shows locked (Restart / Go to map only). No RunState changes — the player's
    /// choice there decides what happens next. See docs/Encounters.md.
    /// </summary>
    public void ResolveDefeat()
    {
        int generation = GameManager.Instance.Generation;
        DoAfterDelay.Execute(() =>
        {
            if (GameManager.IsStale(generation)) return;
            OnDefeatResolved?.Invoke();
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
            MapManager.Instance.EnterMap();
        else
            SceneManager.LoadScene(SceneNames.MapScene);
    }
}
