using System;
using System.Collections;
using UnityEngine;

namespace Game._Scripts.Global
{
    public enum ActiveSide { Player, Enemy }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public ActiveSide ActiveSide { get; private set; } = ActiveSide.Player;
        public void SetActiveSide(ActiveSide side) => ActiveSide = side;

        /// <summary>True during round 1 — read by ShouldSummonCreaturesDecision's NO-STUPID
        /// first-turn rule (see docs/AI.md).</summary>
        public bool IsFirstRound { get; private set; } = true;

        /// <summary>1-indexed round counter — one round is the player's turn AND the enemy's turn.
        /// A triple's bonus-turn re-entry (TakeTurn()'s do-while) does not advance it. Read by
        /// FightSO's per-round LudoProgressIndex override table (see docs/SlotMachine.md).</summary>
        public int CurrentRound { get; private set; } = 1;

        public int Generation { get; private set; }
        public static bool IsStale(int capturedGeneration) => capturedGeneration != Instance.Generation;

        /// <summary>Fired synchronously from <see cref="RestartBattle"/>, before the round loop
        /// restarts. Any script that spawns entities or holds battle-scoped state subscribes
        /// here (in Start(), unsubscribing in OnDestroy()) with its own reset method — see
        /// docs/GameLoop.md.</summary>
        public static event Action OnBattleRestart;

        private GameState _currentState;
        private Coroutine _mainCoroutine;
        private bool _gameOver;
        private bool _bossPhaseTransition;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Creatures.Hero.OnHeroDied += HandleHeroDied;
            _mainCoroutine = StartCoroutine(RunGameLoop());
        }

        void OnDestroy()
        {
            Creatures.Hero.OnHeroDied -= HandleHeroDied;
        }

        // ============================================================
        //  Round script — single source of truth for game-flow order.
        //  Read top-to-bottom to see exactly what happens in a round.
        //  No dynamic List<GameState> insertions: every step is here.
        // ============================================================

        private IEnumerator RunGameLoop()
        {
            yield return Run(new GameStartState());
            OpenContinuationFight();
            yield return RunRounds();
        }

        /// <summary>
        /// A boss phase entered *directly* (a debug profile pointing at its index) never ran the
        /// transition that normally precedes it, so it would otherwise open like an ordinary fight:
        /// empty enemy board, round-1 battle exemption. This reproduces the state
        /// BossPhaseTransitionState would have left behind — the boss already has his trio out, and
        /// the player's first turn is followed by a battle. No-op for an ordinary fight, and never
        /// reached on the real transition path (that re-enters RunRounds directly, not this).
        /// Runs after GameStartState so CampaignStateManager has definitely applied the fight's
        /// roster to G.EnemyCreatures. See docs/GameLoop.md.
        /// </summary>
        private void OpenContinuationFight()
        {
            if (!CampaignStateManager.Instance.CurrentFight.continuesPreviousFight) return;

            BossPhaseTransitionState.SpawnEnemyCreatures();
            IsFirstRound = false;
        }

        /// <summary>
        /// The round loop proper. Split out of <see cref="RunGameLoop"/> so a boss phase transition
        /// can re-enter it without replaying the intro — see <see cref="RunBossPhaseTransition"/>.
        /// Those two are the only entry points; no state ever inserts itself into the sequence.
        /// </summary>
        private IEnumerator RunRounds()
        {
            while (!_gameOver)
            {
                // Player turn. On round 1 the post-turn battle is skipped — the player just
                // summoned units and shouldn't immediately attack.
                yield return PlaySide(ActiveSide.Player, runBattleAfter: !IsFirstRound);
                if (_gameOver) yield break;

                // Enemy turn. Battle always runs after, including on round 1.
                yield return PlaySide(ActiveSide.Enemy, runBattleAfter: true);
                if (_gameOver) yield break;

                yield return Run(new EndOfRoundState());
                IsFirstRound = false;
                CurrentRound++;
            }
        }

        /// <summary>
        /// The enemy hero died but its fight has another phase: play the transition (which swaps the
        /// whole enemy side over to that phase's FightSO), then resume the ordinary round loop from
        /// the player's turn. Entered from <see cref="TryStartBossPhaseTransition"/>, never from a
        /// state. See docs/GameLoop.md.
        /// </summary>
        private IEnumerator RunBossPhaseTransition()
        {
            yield return Run(new BossPhaseTransitionState());
            _bossPhaseTransition = false;
            yield return RunRounds();
        }

        private IEnumerator PlaySide(ActiveSide side, bool runBattleAfter)
        {
            yield return Run(new SwitchSideState(side));
            yield return TakeTurn();
            if (runBattleAfter) yield return RunBattle();
        }

        /// <summary>
        /// One side's turn: roll → action (nuke OR spawn — chosen polymorphically via
        /// <see cref="ActionState"/>). On a triple roll, re-roll and replay the action.
        /// </summary>
        private IEnumerator TakeTurn()
        {
            do
            {
                yield return Run(new RollState());
                yield return Run(CreateActionState());
            }
            while (RollStateManager.Instance.TripleRolled);
        }

        private IEnumerator RunBattle()
        {
            yield return Run(new BattleState());
            yield return Run(new PostBattleState());
        }

        /// <summary>
        /// Polymorphic action factory: returns the matching <see cref="ActionState"/> subclass
        /// for the just-finished roll. Both subclasses self-contain their cleanup (dead-body
        /// removal lives inside <c>NukeState</c>), so no extra "post" state is needed in the
        /// round script above.
        /// </summary>
        private static ActionState CreateActionState()
        {
            if (RollStateManager.Instance.LastRollType == SlotMachine.RollType.Nuke)
                return new NukeState();
            if (RollStateManager.Instance.LastRollType == SlotMachine.RollType.Spell)
                return new SpellState();
            return new SpawningState();
        }

        // ============================================================
        //  State runner: starts a state, waits for completion, cleans up.
        // ============================================================

        private IEnumerator Run(GameState state)
        {
            _currentState = state;
            bool done = false;
            Action onDone = () => done = true;
            state.OnStateCompleted += onDone;

            state.OnStateStart();
            while (!done) yield return null;

            state.OnStateCompleted -= onDone;
            state.OnStateEnd();
            _currentState = null;
        }

        // ============================================================
        //  Game over — interrupts the main loop on hero death.
        // ============================================================

        private void HandleHeroDied(Creatures.Hero hero)
        {
            if (_gameOver) return;
            if (!IsGameOver()) return;
            if (TryStartBossPhaseTransition()) return;

            _gameOver = true;
            AbandonCurrentRun();

            new GameOverState().OnStateStart();
        }

        /// <summary>
        /// Diverts a would-be game over into a boss's next phase when the dead hero's fight has one.
        /// Returns true if it took over — including while a transition is already running, so a
        /// second hero-death event during the swap is swallowed rather than restarting it.
        /// </summary>
        private bool TryStartBossPhaseTransition()
        {
            if (_bossPhaseTransition) return true;
            if (!BossPhaseTransitionState.IsAvailable()) return false;

            _bossPhaseTransition = true;
            AbandonCurrentRun();
            // Phase 2 resumes mid-fight, so the player's turn must be followed by a battle — round 1's
            // "just summon, don't get attacked" exemption doesn't apply any more.
            IsFirstRound = false;
            SetActiveSide(ActiveSide.Player);

            _mainCoroutine = StartCoroutine(RunBossPhaseTransition());
            return true;
        }

        /// <summary>Stops the round coroutine and force-ends whatever state it was in. Shared by
        /// game over, the boss phase transition and restart — all three abandon the run in flight.</summary>
        private void AbandonCurrentRun()
        {
            if (_mainCoroutine != null)
            {
                StopCoroutine(_mainCoroutine);
                _mainCoroutine = null;
            }

            _currentState?.OnStateEnd();
            _currentState = null;
        }

        /// <summary>
        /// The moment either hero dies, the battle is over — remaining creatures on either side
        /// don't matter. Each encounter's enemy hero is effectively the boss; killing it ends the
        /// fight immediately rather than requiring every summoned creature to be cleared too.
        /// </summary>
        private bool IsGameOver()
        {
            return G.PlayerHero.Health.IsDead() || G.EnemyHero.Health.IsDead();
        }

        // ============================================================
        //  Restart — resets both sides and starts a fresh round loop.
        // ============================================================

        public void RestartBattle()
        {
            Generation++; // invalidate every in-flight closure scheduled before this point

            AbandonCurrentRun();
            _gameOver = false;
            _bossPhaseTransition = false;
            SetActiveSide(ActiveSide.Player);
            IsFirstRound = true;
            CurrentRound = 1;

            OnBattleRestart?.Invoke();

            _mainCoroutine = StartCoroutine(RunGameLoop());
        }
    }
}