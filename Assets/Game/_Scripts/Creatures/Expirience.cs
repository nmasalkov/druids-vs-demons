using TMPro;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    public class Experience : MonoBehaviour
    {
        [field: SerializeField] public int Level { get; private set; } = 1;
        [field: SerializeField] public int TotalExperience { get; private set; }
        [SerializeField] private TMP_Text levelText;

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

            Level = targetLevel;
            var stats = creature.Data.Stats(Level);
            creature.Health.Init(stats.health);
            UpdateLevelText();
        }

        /// <summary>
        /// Force-promotes to next level (used by balance tool). Ignores XP.
        /// </summary>
        public void Promote()
        {
            Level++;
            var stats = creature.Data.Stats(Level);
            creature.Health.Init(stats.health);
            UpdateLevelText();
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