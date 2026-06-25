using Game._Scripts.PlayerView;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    /// <summary>
    /// Base class for anything that can be targeted in battle — owns a <see cref="Health"/>
    /// sibling, a <see cref="HitFeedback"/> child anchor, a <see cref="UnitSlot"/> placement,
    /// and the standard death / cleanup / summon feedback hooks. <see cref="Unit"/> extends
    /// this with animator + statuses; <see cref="Shield"/> extends it without.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public abstract class Targetable : MonoBehaviour
    {
        [SerializeField] private MMF_Player onSummonFeedback;
        [SerializeField] private MMF_Player onDeathFeedback;
        [SerializeField] private MMF_Player onBodyCleanUpFeedback;

        public Health Health { get; private set; }
        /// <summary>
        /// Cached reference to the <see cref="HitFeedback"/> MonoBehaviour that lives on a
        /// mandatory child GameObject (every Targetable prefab must contain it).
        /// </summary>
        public HitFeedback HitFeedback { get; private set; }
        public UnitSlot Slot { get; set; }

        protected virtual void Awake()
        {
            Health = GetComponent<Health>();
            HitFeedback = GetComponentInChildren<HitFeedback>();
        }

        protected virtual void Start()
        {
            Health.onDeath += HandleDeath;
        }

        /// <summary>
        /// Called once when the target is summoned/placed into its slot. Plays the optional
        /// summon feedback and lets subclasses hook in extra spawn presentation.
        /// </summary>
        public virtual void OnSummon()
        {
            if (onSummonFeedback != null)
                onSummonFeedback.PlayFeedbacks();
        }

        protected virtual void HandleDeath()
        {
            if (onDeathFeedback != null)
                onDeathFeedback.PlayFeedbacks();
        }

        public virtual void DestroyUnit()
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

