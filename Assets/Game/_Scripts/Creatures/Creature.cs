using _Scripts.Creatures;
using Game._Scripts.PlayerView;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    [RequireComponent(typeof(CreatureAnimator))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Experience))]
    public class Creature : MonoBehaviour
    {
        [field: SerializeField] public CreatureSO Data { get; private set; }
        [SerializeField] private MMF_Player onDeathFeedback;
        [SerializeField] private MMF_Player onBodyCleanUpFeedback;
        [SerializeField] private TMP_Text levelIndicator;

        public CreatureAnimator Animator { get; private set; }
        public Health Health { get; private set; }
        public Experience Experience { get; private set; }
        public CreatureSlot Slot { get; set; }

        void Awake()
        {
            Animator = GetComponent<CreatureAnimator>();
            Health = GetComponent<Health>();
            Experience = GetComponent<Experience>();
        }

        void Start()
        {
            var stats = Data.Stats(Experience.Level);
            Health.Init(stats.health);
            Health.onDeath += HandleDeath;
        }

        private void HandleDeath()
        {
            if (onDeathFeedback != null)
                onDeathFeedback.PlayFeedbacks();
        }

        public void DestroyCreature()
        {
            Slot.Creature = null;

            if (onBodyCleanUpFeedback != null)
            {
                onBodyCleanUpFeedback.PlayFeedbacks();
                onBodyCleanUpFeedback.Events.OnComplete.AddListener(() => Destroy(gameObject));
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}