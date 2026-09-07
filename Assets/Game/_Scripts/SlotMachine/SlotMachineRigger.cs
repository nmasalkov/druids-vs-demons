using System;
using System.Collections.Generic;
using Game._Scripts.Global;
using UnityEngine;

/// <summary>
/// Controllable rigging layer for slot machine rolls. Decides every column's result (the "backend"
/// half of the roll pipeline — SlotMachine/SlotColumn only visualize an already-decided result).
///
/// Terminology (see docs/SlotMachine.md):
/// - Dirty triple situation: 2 of 3 slots already decided to the same ActionSO; the 3rd slot's
///   decision determines whether the roll becomes a full triple.
/// - Dirty Triple Index (PlayerDirtyTripleIndex/EnemyDirtyTripleIndex): per-side 0-200 coefficient
///   controlling the odds a dirty triple situation resolves into a full triple. 100 = neutral
///   (genuine unbiased 1-in-3), 0 = never, 200 = always.
/// - Clean Triple Index (PlayerCleanTripleIndex/EnemyCleanTripleIndex): per-side 0-200 coefficient,
///   same curve as Dirty, checked once per fresh roll (DecideFullRoll only) — success short-circuits
///   straight into an instant all-3-matching triple before any per-slot decision runs at all. On
///   failure, the fresh roll's first two decided slots are forced to differ from each other, so a
///   full triple can never happen "by accident" on a fresh roll — only via this deliberate check.
///   Forced to firstRoundCleanTripleIndex (mirroring firstRoundDirtyTripleIndex) during round 1.
/// - HP-Based Adjustment one-time boost: a POSITIVE HP-based adjustment baked into the acting side's
///   Dirty Triple Index at turn start (see HpAdjustment below) only helps land that turn's FIRST
///   triple — it's stripped back out (RevertPendingHpBoostForActiveSide) the instant that side
///   re-enters its first bonus turn, before Dirty Triple Stabilization is even applied. A NEGATIVE
///   adjustment (a penalty) is never tracked for removal and stays applied the whole turn. Prevents
///   a large comeback boost from fueling a snowballing streak of repeated triples with no
///   stabilization configured to bring it back down.
/// - Dirty Triple Stabilization: FightSO.dirtyTripleStabilization, subtracted from the acting side's
///   Dirty Triple Index each time they re-enter a bonus turn from a triple; reset to a fresh
///   HP-adjusted base on their next genuinely new turn.
/// - Clean Triple Stabilization: FightSO.playerCleanTripleStabilization/enemyCleanTripleStabilization
///   (per-side fields, unlike the single shared dirtyTripleStabilization), a SIGNED delta ADDED to
///   the acting side's Clean Triple Index each time they re-enter a bonus turn from a triple (dirty
///   or clean — either kind grants the bonus turn) — a negative value (the intended usage) decreases
///   it, unlike Dirty Triple Stabilization above which is always a positive magnitude subtracted.
///   Reset back to FightSO.playerCleanTripleIndex/enemyCleanTripleIndex on their next genuinely new
///   turn. Independent lever from Dirty Triple Stabilization — the two indices stabilize on their
///   own separate tracks, and only the Dirty index gets HP-based adjustment (see HpAdjustment
///   below) — Clean Triple Index is a flat per-fight base otherwise.
/// - Ludo Progress Index: FightSO.ludoProgressIndex (+ per-round overrides), added to the acting
///   side's Dirty Triple Index after each reroll during that side's first (non-bonus) roll phase of
///   a turn — undone the moment that roll phase ends, and disabled for the rest of the turn once
///   that side has already earned one triple.
/// </summary>
public class SlotMachineRigger : MonoBehaviour
{
    public static SlotMachineRigger Instance { get; private set; }

    [Header("Live Dirty Triple Index")]
    public int PlayerDirtyTripleIndex = 100;
    public int EnemyDirtyTripleIndex = 100;

    [Header("Live Clean Triple Index")]
    [Tooltip("Chance (0-200, same curve as Dirty) that a fresh roll short-circuits into an instant, " +
             "all-3-matching clean triple before any per-slot decision runs. 100 = neutral 33.3%, " +
             "0 = never, 200 = always. Checked once per fresh roll (DecideFullRoll only) — never " +
             "during a reroll. Overridden per-fight via FightSO at fight start.")]
    public int PlayerCleanTripleIndex = 100;
    public int EnemyCleanTripleIndex = 100;

    [Header("First-Round Override")]
    [Tooltip("Forced value for whichever side starts a turn while GameManager.IsFirstRound is still " +
             "true (covers both sides' opening turns) — skips the HP-based formula entirely. " +
             "Overridden per-fight via FightSO.firstRoundDirtyTripleIndex at fight start.")]
    public int firstRoundDirtyTripleIndex = 50;
    [Tooltip("Clean Triple Index counterpart to firstRoundDirtyTripleIndex above — forced value for " +
             "whichever side starts a turn while GameManager.IsFirstRound is still true, skipping " +
             "playerCleanTripleIndex/enemyCleanTripleIndex entirely for round 1. Overridden per-fight " +
             "via FightSO.firstRoundCleanTripleIndex at fight start.")]
    public int firstRoundCleanTripleIndex = 50;

    [Header("Neutral Baseline")]
    [Tooltip("The HP-formula's starting point before adjustments. 100 = genuine unbiased 1-in-3 " +
             "odds. Overridden per-fight via FightSO.neutralDirtyTripleIndex at fight start.")]
    public int neutralDirtyTripleIndex = 100;

    [Header("HP-Based Adjustment — Player (most-severe-tier-wins, not cumulative)")]
    [Tooltip("Turns the whole HP-based adjustment mechanism off for this side — base index just " +
             "stays at the neutral baseline on any non-first-round turn. Overridden per-fight via " +
             "FightSO at fight start.")]
    public bool playerHpAdjustmentsEnabled = true;
    public HpAdjustmentSettings playerHpAdjustment = HpAdjustmentSettings.Default;

    [Header("HP-Based Adjustment — Enemy (most-severe-tier-wins, not cumulative)")]
    [Tooltip("Turns the whole HP-based adjustment mechanism off for this side — base index just " +
             "stays at the neutral baseline on any non-first-round turn. Overridden per-fight via " +
             "FightSO at fight start.")]
    public bool enemyHpAdjustmentsEnabled = true;
    public HpAdjustmentSettings enemyHpAdjustment = HpAdjustmentSettings.Default;

    [Header("Clamp")]
    [SerializeField] private int minDirtyTripleIndex = 0;
    [SerializeField] private int maxDirtyTripleIndex = 200;

    [Header("Ludo Progress (reroll pity — debug view only, authored on FightSO)")]
    [Tooltip("How much of the active side's Dirty Triple Index has been added by Ludo Progress so " +
             "far this roll phase — undone the moment the roll phase ends. Runtime-only, never " +
             "authored here.")]
    [SerializeField] private int _ludoBumpApplied;

    private bool _justSwitchedSide;
    private bool _ludoProgressEligible;
    private int _playerPendingHpBoost;
    private int _enemyPendingHpBoost;
    private readonly List<(SlotColumn column, Action handler)> _rerollSubscriptions = new();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameState.OnAnyStateEnded += HandleStateEnded;
        GameState.OnAnyStateStarted += HandleStateStarted;
        GameManager.OnBattleRestart += ResetForRestart;
        ApplyFightOverrides();
        SubscribeToRerolls();
    }

    void OnDestroy()
    {
        GameState.OnAnyStateEnded -= HandleStateEnded;
        GameState.OnAnyStateStarted -= HandleStateStarted;
        GameManager.OnBattleRestart -= ResetForRestart;
        foreach (var (column, handler) in _rerollSubscriptions)
            column.OnRerollStarted -= handler;
    }

    // ============================================================
    //  Per-fight config — read from FightSO, applied to the live fields above.
    // ============================================================

    private void ApplyFightOverrides()
    {
        var fight = CampaignStateManager.Instance.CurrentFight;
        neutralDirtyTripleIndex = fight.neutralDirtyTripleIndex;
        firstRoundDirtyTripleIndex = fight.firstRoundDirtyTripleIndex;
        firstRoundCleanTripleIndex = fight.firstRoundCleanTripleIndex;
        PlayerCleanTripleIndex = fight.playerCleanTripleIndex;
        EnemyCleanTripleIndex = fight.enemyCleanTripleIndex;
        playerHpAdjustmentsEnabled = fight.playerHpAdjustmentsEnabled;
        playerHpAdjustment = fight.playerHpAdjustment;
        enemyHpAdjustmentsEnabled = fight.enemyHpAdjustmentsEnabled;
        enemyHpAdjustment = fight.enemyHpAdjustment;
    }

    // ============================================================
    //  Turn-boundary bookkeeping
    // ============================================================

    private void HandleStateEnded(GameState state)
    {
        if (state is SwitchSideState)
        {
            RecomputeBaseForActiveSide();
            ResetCleanBaseForActiveSide();
            _justSwitchedSide = true;
            return;
        }
        if (state is RollState)
            UndoLudoProgressBump();
    }

    private void HandleStateStarted(GameState state)
    {
        if (state is not RollState) return;
        if (_justSwitchedSide)
        {
            _justSwitchedSide = false;
            _ludoProgressEligible = true;
            return;
        }
        _ludoProgressEligible = false;
        RevertPendingHpBoostForActiveSide();
        ApplyStabilizationForActiveSide();
        ApplyCleanStabilizationForActiveSide();
    }

    private void RecomputeBaseForActiveSide()
    {
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        int adjustment = 0;
        int value = GameManager.Instance.IsFirstRound
            ? firstRoundDirtyTripleIndex
            : ComputeHpAdjustedBase(isPlayer, out adjustment);
        SetIndex(isPlayer, value);
        // Only a POSITIVE adjustment (a comeback boost) is earmarked for one-time removal on this
        // side's first bonus turn — see RevertPendingHpBoostForActiveSide. A negative adjustment (a
        // penalty, e.g. high-HP sides) is never tracked here, so it stays applied for the whole turn.
        SetPendingHpBoost(isPlayer, Mathf.Max(adjustment, 0));
    }

    private int ComputeHpAdjustedBase(bool isPlayer, out int adjustment)
    {
        bool enabled = isPlayer ? playerHpAdjustmentsEnabled : enemyHpAdjustmentsEnabled;
        if (!enabled)
        {
            adjustment = 0;
            return neutralDirtyTripleIndex;
        }

        var settings = isPlayer ? playerHpAdjustment : enemyHpAdjustment;
        float hp = (isPlayer ? G.PlayerHero : G.EnemyHero).Health.HealthPercent;
        adjustment = HpAdjustment(settings, hp);
        return Clamp(neutralDirtyTripleIndex + adjustment);
    }

    private int HpAdjustment(HpAdjustmentSettings s, float hp)
    {
        if (hp < s.nearDeathHpThreshold) return s.nearDeathHpAdjustment;
        if (hp < s.criticalHpThreshold) return s.criticalHpAdjustment;
        if (hp < s.lowHpThreshold) return s.lowHpAdjustment;
        if (hp > s.highHpThreshold) return s.highHpAdjustment;
        return 0;
    }

    private void ApplyStabilizationForActiveSide()
    {
        var fight = CampaignStateManager.Instance.CurrentFight;
        if (fight.dirtyTripleStabilization == 0) return;

        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        int current = isPlayer ? PlayerDirtyTripleIndex : EnemyDirtyTripleIndex;
        SetIndex(isPlayer, Clamp(current - fight.dirtyTripleStabilization));
    }

    private void SetIndex(bool isPlayer, int value)
    {
        if (isPlayer) PlayerDirtyTripleIndex = value;
        else EnemyDirtyTripleIndex = value;
    }

    private void SetPendingHpBoost(bool isPlayer, int value)
    {
        if (isPlayer) _playerPendingHpBoost = value;
        else _enemyPendingHpBoost = value;
    }

    /// <summary>
    /// One-time removal of a POSITIVE HP-based boost baked into the acting side's Dirty Triple Index at
    /// this turn's start, the moment that side re-enters its first bonus turn (i.e. right after landing
    /// its first triple this turn). A negative adjustment (a penalty, e.g. high-HP sides) is never
    /// tracked here and stays applied for the whole turn — only a positive "comeback" boost gets
    /// stripped back out, so it can help land the FIRST triple of a turn without also fueling a
    /// snowballing streak of further ones. See docs/SlotMachine.md.
    /// </summary>
    private void RevertPendingHpBoostForActiveSide()
    {
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        int pending = isPlayer ? _playerPendingHpBoost : _enemyPendingHpBoost;
        if (pending == 0) return;

        int current = isPlayer ? PlayerDirtyTripleIndex : EnemyDirtyTripleIndex;
        SetIndex(isPlayer, Clamp(current - pending));
        SetPendingHpBoost(isPlayer, 0);
    }

    /// <summary>
    /// Resets the acting side's Clean Triple Index back to the fight's flat authored base
    /// (FightSO.playerCleanTripleIndex/enemyCleanTripleIndex) at the start of a genuinely new turn —
    /// the Clean-index counterpart to RecomputeBaseForActiveSide, undoing any Clean Triple
    /// Stabilization applied during the previous turn's bonus-turn streak. Unlike the Dirty base,
    /// this has no HP-based formula to compute — it's always the same flat per-fight number, except
    /// during round 1, where firstRoundCleanTripleIndex takes over instead (mirroring
    /// firstRoundDirtyTripleIndex).
    /// </summary>
    private void ResetCleanBaseForActiveSide()
    {
        var fight = CampaignStateManager.Instance.CurrentFight;
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        int value = GameManager.Instance.IsFirstRound
            ? firstRoundCleanTripleIndex
            : (isPlayer ? fight.playerCleanTripleIndex : fight.enemyCleanTripleIndex);
        SetCleanIndex(isPlayer, Clamp(value));
    }

    /// <summary>
    /// Clean-index counterpart to ApplyStabilizationForActiveSide — same bonus-turn trigger (a
    /// RollState re-entry that isn't a fresh SwitchSideState turn), but reads
    /// FightSO.playerCleanTripleStabilization/enemyCleanTripleStabilization (per-side fields, unlike
    /// the single shared FightSO.dirtyTripleStabilization) as a SIGNED delta ADDED to the acting
    /// side's Clean Triple Index, not a positive magnitude subtracted (unlike Dirty Triple
    /// Stabilization above) — a negative value (the intended, "stabilizing" case) decreases the
    /// index, a positive value would increase it. The two stabilizations are independent: both can
    /// apply to the same bonus-turn re-entry, each against its own index and its own sign convention.
    /// </summary>
    private void ApplyCleanStabilizationForActiveSide()
    {
        var fight = CampaignStateManager.Instance.CurrentFight;
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        int stabilization = isPlayer ? fight.playerCleanTripleStabilization : fight.enemyCleanTripleStabilization;
        if (stabilization == 0) return;

        int current = isPlayer ? PlayerCleanTripleIndex : EnemyCleanTripleIndex;
        SetCleanIndex(isPlayer, Clamp(current + stabilization));
    }

    private void SetCleanIndex(bool isPlayer, int value)
    {
        if (isPlayer) PlayerCleanTripleIndex = value;
        else EnemyCleanTripleIndex = value;
    }

    private int Clamp(int value) => Mathf.Clamp(value, minDirtyTripleIndex, maxDirtyTripleIndex);

    private void ResetForRestart()
    {
        PlayerDirtyTripleIndex = neutralDirtyTripleIndex;
        EnemyDirtyTripleIndex = neutralDirtyTripleIndex;
        _justSwitchedSide = false;
        _ludoProgressEligible = false;
        _ludoBumpApplied = 0;
        _playerPendingHpBoost = 0;
        _enemyPendingHpBoost = 0;
        ApplyFightOverrides();
    }

    // ============================================================
    //  Ludo Progress — reroll pity escalation of the active side's Dirty Triple Index.
    // ============================================================

    private void SubscribeToRerolls()
    {
        // Both machines can still be inactive at this point (only the active side's machine
        // GameObject is enabled, toggled later by RollStateManager) — must include inactive ones
        // or this silently never finds either machine.
        foreach (var machine in FindObjectsByType<SlotMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool isPlayer = machine.IsPlayerMachine;
            foreach (var column in machine.Columns)
            {
                void Handler() => HandleRerollStarted(isPlayer);
                column.OnRerollStarted += Handler;
                _rerollSubscriptions.Add((column, Handler));
            }
        }
    }

    private void HandleRerollStarted(bool isPlayer)
    {
        if (!_ludoProgressEligible) return;

        int ludoProgress = CampaignStateManager.Instance.CurrentFight
            .GetLudoProgressIndexForRound(GameManager.Instance.CurrentRound);
        if (ludoProgress == 0) return;

        SetIndex(isPlayer, Clamp((isPlayer ? PlayerDirtyTripleIndex : EnemyDirtyTripleIndex) + ludoProgress));
        _ludoBumpApplied += ludoProgress;
    }

    private void UndoLudoProgressBump()
    {
        if (_ludoBumpApplied == 0) return;
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        SetIndex(isPlayer, Clamp((isPlayer ? PlayerDirtyTripleIndex : EnemyDirtyTripleIndex) - _ludoBumpApplied));
        _ludoBumpApplied = 0;
    }

    // ============================================================
    //  Roll-decision API — pure, headless, no visual coupling.
    // ============================================================

    /// <summary>
    /// Decides all 3 slots for a fresh roll. First checks the Clean Triple Index for an instant
    /// short-circuit into a full triple; otherwise shuffles which physical slot gets decided
    /// 1st/2nd/3rd so no single column is ever consistently "the one that completes triples," and
    /// forces the 2nd decided slot to differ from the 1st (see class summary).
    /// </summary>
    public ActionSO[] DecideFullRoll(ActionSO[] options, bool isPlayerSide)
    {
        if (RollForCleanTriple(isPlayerSide))
            return FullTripleOf(PickRandom(options));

        var order = ShuffledOrder();
        var results = new ActionSO[3];
        results[order[0]] = PickRandom(options);
        results[order[1]] = PickOtherThan(options, results[order[0]]);
        results[order[2]] = DecideConditional(options, results[order[0]], results[order[1]], isPlayerSide);
        return results;
    }

    /// <summary>Decides a single rerolled slot given the other 2 columns' current results.</summary>
    public ActionSO DecideRerollSlot(ActionSO[] options, ActionSO otherA, ActionSO otherB, bool isPlayerSide)
        => DecideConditional(options, otherA, otherB, isPlayerSide);

    private bool RollForCleanTriple(bool isPlayerSide)
    {
        int index = isPlayerSide ? PlayerCleanTripleIndex : EnemyCleanTripleIndex;
        return AIController.RollForProbability(Mathf.RoundToInt(TripleChancePercent(index)));
    }

    private static ActionSO[] FullTripleOf(ActionSO action) => new[] { action, action, action };

    private ActionSO DecideConditional(ActionSO[] options, ActionSO otherA, ActionSO otherB, bool isPlayerSide)
    {
        if (otherA != otherB) return PickRandom(options);

        int index = isPlayerSide ? PlayerDirtyTripleIndex : EnemyDirtyTripleIndex;
        bool triple = AIController.RollForProbability(Mathf.RoundToInt(TripleChancePercent(index)));
        return triple ? otherA : PickOtherThan(options, otherA);
    }

    private static float TripleChancePercent(int index)
    {
        index = Mathf.Clamp(index, 0, 200);
        return index <= 100 ? index / 3f : 100f / 3f + (index - 100) * (2f / 3f);
    }

    private static ActionSO PickRandom(ActionSO[] options) => options[UnityEngine.Random.Range(0, options.Length)];

    private static ActionSO PickOtherThan(ActionSO[] options, ActionSO exclude)
    {
        Span<int> candidateIndices = stackalloc int[options.Length];
        int count = 0;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == exclude) continue;
            candidateIndices[count++] = i;
        }
        return options[candidateIndices[UnityEngine.Random.Range(0, count)]];
    }

    private static int[] ShuffledOrder()
    {
        var order = new[] { 0, 1, 2 };
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }
}
