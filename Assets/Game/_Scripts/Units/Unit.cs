using _Scripts.Creatures;
using Game._Scripts.PlayerView;
using Game._Scripts.Units;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(StatusesManager))]
    public abstract class Unit : MonoBehaviour
    {
        [SerializeField] private MMF_Player onDeathFeedback;
        [SerializeField] private MMF_Player onBodyCleanUpFeedback;

        public UnitAnimator Animator { get; private set; }
        public Health Health { get; private set; }
        public StatusesManager StatusesManager { get; private set; }
        /// <summary>
        /// Cached reference to the <see cref="HitFeedback"/> MonoBehaviour that lives on the
        /// mandatory child GameObject named "HitFeedback" (every Unit prefab must contain it).
        /// </summary>
        public HitFeedback HitFeedback { get; private set; }
        public UnitSlot Slot { get; set; }

        protected virtual void Awake()
        {
            Animator = GetComponent<UnitAnimator>();
            Health = GetComponent<Health>();
            StatusesManager = GetComponent<StatusesManager>();
            HitFeedback = GetComponentInChildren<HitFeedback>();
        }

        protected virtual void Start()
        {
            Health.onDeath += HandleDeath;
        }

        protected abstract float GetMaxHealth();

        public void InitHealth()
        {
            Health.Init(GetMaxHealth());
        }

        private void HandleDeath()
        {
            if (onDeathFeedback != null)
                onDeathFeedback.PlayFeedbacks();
        }

        public void DestroyUnit()
        {
            Slot.Unit = null;

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



