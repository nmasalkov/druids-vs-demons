using System.Collections.Generic;
using UnityEngine;

namespace Game._Scripts.Global
{
    public enum ActiveSide { Player, Enemy }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public ActiveSide ActiveSide { get; private set; } = ActiveSide.Player;

        public void SetActiveSide(ActiveSide side)
        {
            ActiveSide = side;
        }

        private List<GameState> _states;
        private int _currentStateIndex = -1;
        private bool _gameOver;
        private bool _firstRound = true;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            _states = new List<GameState>
            {
                new GameStartState()
            };

            Creatures.Hero.OnHeroDied += HandleHeroDied;

            AppendRoundStates();
            AdvanceState();
        }

        void OnDestroy()
        {
            Creatures.Hero.OnHeroDied -= HandleHeroDied;
        }

        private void AppendRoundStates()
        {
            _states.Add(new SwitchSideState(ActiveSide.Player));
            _states.Add(new RollState());

            if (!_firstRound)
            {
                _states.Add(new BattleState());
                _states.Add(new PostBattleState());
            }

            _states.Add(new SwitchSideState(ActiveSide.Enemy));
            _states.Add(new RollState());
            _states.Add(new BattleState());
            _states.Add(new PostBattleState());
            _states.Add(new EndOfRoundState());

            _firstRound = false;
        }

        public void AdvanceState()
        {
            if (_gameOver) return;

            if (_currentStateIndex >= 0 && _currentStateIndex < _states.Count)
            {
                _states[_currentStateIndex].OnStateCompleted -= AdvanceState;
                _states[_currentStateIndex].OnStateEnd();
            }

            // After a RollState, insert the proper ActionState based on the captured roll type.
            if (_currentStateIndex >= 0 && _states[_currentStateIndex] is RollState)
            {
                ActionState actionState = RollStateManager.Instance.LastRollType == SlotMachine.RollType.Nuke
                    ? new NukeState()
                    : (ActionState)new SpawningState();
                _states.Insert(_currentStateIndex + 1, actionState);
            }

            // After an ActionState, if triple was rolled, insert another Roll + Action cycle.
            if (_currentStateIndex >= 0 && _states[_currentStateIndex] is ActionState && RollStateManager.Instance.TripleRolled)
            {
                _states.Insert(_currentStateIndex + 1, new RollState());
            }

            // After end of round, check game over or start new round
            if (_currentStateIndex >= 0 && _states[_currentStateIndex] is EndOfRoundState)
            {
                if (TryGameOver()) return;
                AppendRoundStates();
            }

            _currentStateIndex++;

            if (_currentStateIndex < _states.Count)
            {
                _states[_currentStateIndex].OnStateCompleted += AdvanceState;
                _states[_currentStateIndex].OnStateStart();
            }
        }

        private bool IsGameOver()
        {
            bool playerAlive = IsSideAlive(G.PlayerCreaturesManager, G.PlayerHero);
            bool enemyAlive = IsSideAlive(G.EnemyCreaturesManager, G.EnemyHero);
            return !playerAlive || !enemyAlive;
        }

        private bool IsSideAlive(PlayerView.CreaturesManager creatures, Creatures.Hero hero)
        {
            if (hero != null && !hero.Health.IsDead()) return true;
            return creatures.GetAllCreatures().Count > 0;
        }

        private bool TryGameOver()
        {
            if (_gameOver) return true;
            if (!IsGameOver()) return false;

            _gameOver = true;

            if (_currentStateIndex >= 0 && _currentStateIndex < _states.Count)
            {
                _states[_currentStateIndex].OnStateCompleted -= AdvanceState;
                _states[_currentStateIndex].OnStateEnd();
            }

            var gameOverState = new GameOverState();
            _states.Clear();
            _states.Add(gameOverState);
            _currentStateIndex = 0;
            gameOverState.OnStateStart();
            return true;
        }

        private void HandleHeroDied(Creatures.Hero hero)
        {
            TryGameOver();
        }
    }
}