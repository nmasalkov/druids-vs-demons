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

            _gameOver = true;

            if (_mainCoroutine != null)
            {
                StopCoroutine(_mainCoroutine);
                _mainCoroutine = null;
            }

            _currentState?.OnStateEnd();
            _currentState = null;

            new GameOverState().OnStateStart();
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

            if (_mainCoroutine != null)
            {
                StopCoroutine(_mainCoroutine);
                _mainCoroutine = null;
            }
            _currentState?.OnStateEnd();
            _currentState = null;
            _gameOver = false;
            SetActiveSide(ActiveSide.Player);
            IsFirstRound = true;
            CurrentRound = 1;

            OnBattleRestart?.Invoke();

            _mainCoroutine = StartCoroutine(RunGameLoop());
        }
    }
}