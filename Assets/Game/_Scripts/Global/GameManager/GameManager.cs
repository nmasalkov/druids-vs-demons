using System.Collections.Generic;
using UnityEngine;

namespace Game._Scripts.Global
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private List<GameState> _states;
        private int _currentStateIndex = -1;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            _states = new List<GameState>
            {
                new GameStartState(),
                new RollState()
            };

            AdvanceState();
        }

        public void AdvanceState()
        {
            if (_currentStateIndex >= 0 && _currentStateIndex < _states.Count)
            {
                _states[_currentStateIndex].OnStateCompleted -= AdvanceState;
                _states[_currentStateIndex].OnStateEnd();
            }

            _currentStateIndex++;

            if (_currentStateIndex < _states.Count)
            {
                _states[_currentStateIndex].OnStateCompleted += AdvanceState;
                _states[_currentStateIndex].OnStateStart();
            }
        }
    }
}