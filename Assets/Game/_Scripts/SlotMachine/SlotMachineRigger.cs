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
/// - Comeback Adjustment: FightSO.playerComebackSettings/enemyComebackSettings, an authored ladder
///   of (HP%, opponent firepower advantage, triple adjustment) entries. At the start of every
///   genuinely new turn the acting side's ladder is evaluated — an entry matches on HP at-or-below
///   OR on the opponent's effective firepower lead, and the biggest adjustment among matching
///   entries wins — and the result is added to BOTH that side's Dirty and Clean Triple Index. Unlike
///   the HP-only curve this replaced, it is not a Dirty-only lever.
/// - Comeback halving: a POSITIVE comeback adjustment is halved (and both indices lowered by the
///   same delta) each time the acting side re-enters a bonus turn, i.e. every time it lands another
///   triple — so a large comeback boost helps land a turn's first triple without fuelling an endless
///   streak. A NEGATIVE adjustment (the high-HP punish) is never halved and stays applied for the
///   whole turn: halving it would mean landing a triple rewards you by softening your own punish.
/// - Dirty Triple Stabilization: FightSO.dirtyTripleStabilization, subtracted from the acting side's
///   Dirty Triple Index each time they re-enter a bonus turn from a triple; reset to a fresh
///   comeback-adjusted base on their next genuinely new turn.
/// - Clean Triple Stabilization: FightSO.playerCleanTripleStabilization/enemyCleanTripleStabilization
///   (per-side fields, unlike the single shared dirtyTripleStabilization), a SIGNED delta ADDED to
///   the acting side's Clean Triple Index each time they re-enter a bonus turn from a triple (dirty
///   or clean — either kind grants the bonus turn) — a negative value (the intended usage) decreases
///   it, unlike Dirty Triple Stabilization above which is always a positive magnitude subtracted.
///   Reset back to FightSO.playerCleanTripleIndex/enemyCleanTripleIndex (plus the fresh comeback
///   adjustment) on their next genuinely new turn. Independent lever from Dirty Triple
///   Stabilization — the two indices stabilize on their own separate tracks.
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
             "true (covers both sides' opening turns) — skips Comeback Settings entirely. " +
             "Overridden per-fight via FightSO.firstRoundDirtyTripleIndex at fight start.")]
    public int firstRoundDirtyTripleIndex = 50;
    [Tooltip("Clean Triple Index counterpart to firstRoundDirtyTripleIndex above — forced value for " +
             "whichever side starts a turn while GameManager.IsFirstRound is still true, skipping " +
             "playerCleanTripleIndex/enemyCleanTripleIndex (and Comeback Settings) entirely for " +
             "round 1. Overridden per-fight via FightSO.firstRoundCleanTripleIndex at fight start.")]
    public int firstRoundCleanTripleIndex = 50;

    [Header("Neutral Baseline")]
    [Tooltip("The Dirty Triple Index's starting point before the comeback adjustment is added. " +
             "100 = genuine unbiased 1-in-3 odds. Overridden per-fight via " +
             "FightSO.neutralDirtyTripleIndex at fight start.")]
    public int neutralDirtyTripleIndex = 100;

    [Header("Clamp")]
    [SerializeField] private int minDirtyTripleIndex = 0;
    [SerializeField] private int maxDirtyTripleIndex = 200;

    [Header("Live Comeback Adjustment (debug view only, authored on FightSO)")]
    [Tooltip("The comeback adjustment currently baked into the player's Dirty AND Clean Triple " +
             "Index — recomputed from FightSO.playerComebackSettings at the start of every genuinely " +
             "new player turn, then halved on each bonus turn. Negative = the high-HP punish. " +
             "Runtime-only, never authored here.")]
    [SerializeField] private int _playerComebackAdjustment;
    [Tooltip("Enemy-side counterpart to the player's comeback adjustment above, computed from " +
             "FightSO.enemyComebackSettings. Runtime-only, never authored here.")]
    [SerializeField] private int _enemyComebackAdjustment;

    [Header("Ludo Progress (reroll pity — debug view only, authored on FightSO)")]
    [Tooltip("How much of the active side's Dirty Triple Index has been added by Ludo Progress so " +
             "far this roll phase — undone the moment the roll phase ends. Runtime-only, never " +
             "authored here.")]
    [SerializeField] private int _ludoBumpApplied;

    private bool _justSwitchedSide;
    private bool _ludoProgressEligible;
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
        HalveComebackForActiveSide();
        ApplyStabilizationForActiveSide();
        ApplyCleanStabilizationForActiveSide();
    }

    /// <summary>
    /// Recomputes the acting side's Dirty Triple Index base at the start of a genuinely new turn, and
    /// stores the comeback adjustment that went into it — ResetCleanBaseForActiveSide (which runs
    /// immediately after) and the per-bonus-turn halving both reuse that same stored number.
    /// </summary>
    private void RecomputeBaseForActiveSide()
    {
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        if (GameManager.Instance.IsFirstRound)
        {
            SetComebackAdjustment(isPlayer, 0);
            SetIndex(isPlayer, firstRoundDirtyTripleIndex);
            return;
        }

        int adjustment = ComputeComebackAdjustment(isPlayer);
        SetComebackAdjustment(isPlayer, adjustment);
        SetIndex(isPlayer, Clamp(neutralDirtyTripleIndex + adjustment));
    }

    /// <summary>
    /// Evaluates the acting side's authored comeback ladder against its own hero HP% and the
    /// opponent's effective firepower lead. Both inputs are live board state, read fresh here —
    /// neither is cached between turns.
    /// </summary>
    private int ComputeComebackAdjustment(bool isPlayer)
    {
        var hero = isPlayer ? G.PlayerHero : G.EnemyHero;
        int hpPercent = Mathf.RoundToInt(hero.Health.HealthPercent * 100f);
        int advantage = Mathf.RoundToInt(AttacksResolver.OpponentFirepowerAdvantage(isPlayer));
        return CampaignStateManager.Instance.CurrentFight
            .GetComebackAdjustment(isPlayer, hpPercent, advantage);
    }

    /// <summary>
    /// Resets the acting side's Clean Triple Index at the start of a genuinely new turn: the fight's
    /// flat authored base (FightSO.playerCleanTripleIndex/enemyCleanTripleIndex) plus this turn's
    /// comeback adjustment, undoing any Clean Triple Stabilization applied during the previous turn's
    /// bonus-turn streak. Round 1 uses firstRoundCleanTripleIndex instead, mirroring the Dirty base.
    /// Must run after RecomputeBaseForActiveSide — that's what stores the adjustment.
    /// </summary>
    private void ResetCleanBaseForActiveSide()
    {
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        if (GameManager.Instance.IsFirstRound)
        {
            SetCleanIndex(isPlayer, Clamp(firstRoundCleanTripleIndex));
            return;
        }

        var fight = CampaignStateManager.Instance.CurrentFight;
        int authored = isPlayer ? fight.playerCleanTripleIndex : fight.enemyCleanTripleIndex;
        SetCleanIndex(isPlayer, Clamp(authored + ComebackAdjustment(isPlayer)));
    }

    /// <summary>
    /// Halves a POSITIVE comeback adjustment the moment the acting side re-enters a bonus turn (i.e.
    /// right after landing another triple), lowering both the Dirty and Clean Triple Index by the
    /// same delta — so a comeback boost decays across a streak (70 -> 35 -> 17 -> ...) instead of
    /// staying at full strength for every triple in it. A NEGATIVE adjustment (the high-HP punish) is
    /// left alone and stays applied for the whole turn; halving it would turn landing a triple into
    /// its own reward. See docs/SlotMachine.md.
    /// </summary>
    private void HalveComebackForActiveSide()
    {
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        int current = ComebackAdjustment(isPlayer);
        if (current <= 0) return;

        int halved = current / 2;
        int delta = current - halved;
        SetIndex(isPlayer, Clamp(DirtyIndex(isPlayer) - delta));
        SetCleanIndex(isPlayer, Clamp(CleanIndex(isPlayer) - delta));
        SetComebackAdjustment(isPlayer, halved);
    }

    private void ApplyStabilizationForActiveSide()
    {
        var fight = CampaignStateManager.Instance.CurrentFight;
        if (fight.dirtyTripleStabilization == 0) return;

        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        SetIndex(isPlayer, Clamp(DirtyIndex(isPlayer) - fight.dirtyTripleStabilization));
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

        SetCleanIndex(isPlayer, Clamp(CleanIndex(isPlayer) + stabilization));
    }

    private int DirtyIndex(bool isPlayer) => isPlayer ? PlayerDirtyTripleIndex : EnemyDirtyTripleIndex;

    private void SetIndex(bool isPlayer, int value)
    {
        if (isPlayer) PlayerDirtyTripleIndex = value;
        else EnemyDirtyTripleIndex = value;
    }

    private int CleanIndex(bool isPlayer) => isPlayer ? PlayerCleanTripleIndex : EnemyCleanTripleIndex;

    private void SetCleanIndex(bool isPlayer, int value)
    {
        if (isPlayer) PlayerCleanTripleIndex = value;
        else EnemyCleanTripleIndex = value;
    }

    private int ComebackAdjustment(bool isPlayer) => isPlayer ? _playerComebackAdjustment : _enemyComebackAdjustment;

    private void SetComebackAdjustment(bool isPlayer, int value)
    {
        if (isPlayer) _playerComebackAdjustment = value;
        else _enemyComebackAdjustment = value;
    }

    private int Clamp(int value) => Mathf.Clamp(value, minDirtyTripleIndex, maxDirtyTripleIndex);

    private void ResetForRestart()
    {
        PlayerDirtyTripleIndex = neutralDirtyTripleIndex;
        EnemyDirtyTripleIndex = neutralDirtyTripleIndex;
        _justSwitchedSide = false;
        _ludoProgressEligible = false;
        _ludoBumpApplied = 0;
        _playerComebackAdjustment = 0;
        _enemyComebackAdjustment = 0;
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

        SetIndex(isPlayer, Clamp(DirtyIndex(isPlayer) + ludoProgress));
        _ludoBumpApplied += ludoProgress;
    }

    private void UndoLudoProgressBump()
    {
        if (_ludoBumpApplied == 0) return;
        bool isPlayer = GameManager.Instance.ActiveSide == ActiveSide.Player;
        SetIndex(isPlayer, Clamp(DirtyIndex(isPlayer) - _ludoBumpApplied));
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
        => AIController.RollForProbability(Mathf.RoundToInt(TripleChancePercent(CleanIndex(isPlayerSide))));

    private static ActionSO[] FullTripleOf(ActionSO action) => new[] { action, action, action };

    private ActionSO DecideConditional(ActionSO[] options, ActionSO otherA, ActionSO otherB, bool isPlayerSide)
    {
        if (otherA != otherB) return PickRandom(options);

        bool triple = AIController.RollForProbability(Mathf.RoundToInt(TripleChancePercent(DirtyIndex(isPlayerSide))));
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
