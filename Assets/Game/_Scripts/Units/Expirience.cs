using System;
using TMPro;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    public class Experience : MonoBehaviour
    {
        [field: SerializeField] public int Level { get; private set; } = 1;
        [field: SerializeField] public int TotalExperience { get; private set; }
        [SerializeField] private TMP_Text levelText;

        /// <summary>
        /// Raised whenever <see cref="Level"/> increases (via XP gain, force-promote, or
        /// PromoteToLevel). Used by <c>StatusesManager</c> to clear statuses on level-up.
        /// </summary>
        public event Action OnPromoted;

        private Creature creature;

        void Awake()
        {
            creature = GetComponent<Creature>();
        }

        void Start()
        {
            UpdateLevelText();
        }

        public void AddExperience(int amount)
        {
            TotalExperience += amount;
            HandlePromotion();
        }

        public void HandlePromotion()
        {
            int targetLevel = creature.Data.GetLevelForXp(TotalExperience);
            if (targetLevel <= Level) return;
            ApplyLevel(targetLevel);
        }

        /// <summary>
        /// Force-promotes to next level (used by balance tool). Ignores XP.
        /// </summary>
        public void Promote()
        {
            ApplyLevel(Level + 1);
        }

        /// <summary>
        /// Sets XP to the threshold for the given level and promotes.
        /// Used when rolling matching cards.
        /// </summary>
        public void PromoteToLevel(int targetLevel)
        {
            targetLevel = Mathf.Clamp(targetLevel, 1, creature.Data.xpThresholds.Length);
            if (targetLevel <= Level) return;

            TotalExperience = creature.Data.xpThresholds[targetLevel - 1];
            ApplyLevel(targetLevel);
        }

        private void ApplyLevel(int newLevel)
        {
            Level = newLevel;
            var stats = creature.Data.Stats(Level);
            creature.Health.Init(stats.health);
            UpdateLevelText();
            OnPromoted?.Invoke();
        }

        /// <summary>
        /// Instantly grants XP without animations or gems.
        /// Use for tests and instant resolution of game logic.
        /// </summary>
        public void AddExperienceInstant(int amount)
        {
            AddExperience(amount);
        }

        private void UpdateLevelText()
        {
            levelText.text = Level.ToString();
        }
    }
}