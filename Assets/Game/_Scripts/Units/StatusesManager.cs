using System;
using Game._Scripts.Creatures;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game._Scripts.Units
{
    /// <summary>
    /// Owns runtime status flags (e.g. <see cref="IsShocked"/>) for a <see cref="Unit"/>
    /// and toggles the matching feedback prefabs via events. Auto-added to every Unit via
    /// <c>[RequireComponent]</c> on the base class (rule #14: helper component owned by base).
    /// Statuses are cleared on heal and on level-up so designers don't need extra plumbing.
    /// </summary>
    public class StatusesManager : MonoBehaviour
    {
        [Header("Feedbacks")]
        [Tooltip("MMF_Player on the ShockedFeedback child. Played while IsShocked is true.")]
        [SerializeField] private MMF_Player shockedFeedback;

        public bool IsShocked { get; private set; }

        /// <summary>Fired whenever <see cref="IsShocked"/> flips. Argument is the new value.</summary>
        public event Action<bool> OnShockedChanged;

        private Unit unit;

        void Awake()
        {
            unit = GetComponent<Unit>();
        }

        void Start()
        {
            unit.Health.onHealed += ClearAllStatuses;
            unit.Health.onDeath += ClearAllStatuses;

            // Creatures additionally lose statuses when they level up.
            var experience = GetComponent<Experience>();
            if (experience != null)
                experience.OnPromoted += ClearAllStatuses;
        }

        public void ApplyShock() => SetShocked(true);
        public void ClearShock() => SetShocked(false);

        private void SetShocked(bool value)
        {
            if (IsShocked == value) return;
            IsShocked = value;

            if (value) shockedFeedback.PlayFeedbacks();
            else shockedFeedback.StopFeedbacks();

            OnShockedChanged?.Invoke(value);
        }

        private void ClearAllStatuses()
        {
            ClearShock();
        }
    }
}


