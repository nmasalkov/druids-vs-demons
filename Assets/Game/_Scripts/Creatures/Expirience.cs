using TMPro;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    public class Experience : MonoBehaviour
    {
        [field: SerializeField] public int Level { get; private set; } = 1;
        [SerializeField] private int experience;
        [SerializeField] private TMP_Text levelText;

        void Start()
        {
            UpdateLevelText();
        }

        public void Promote()
        {
            Level++;
            var creature = GetComponent<Creature>();
            var stats = creature.Data.Stats(Level);
            creature.Health.Init(stats.health);
            UpdateLevelText();
        }

        private void UpdateLevelText()
        {
            levelText.text = Level.ToString();
        }
    }
}