using UnityEngine;

namespace Game._Scripts.Creatures
{
    /// <summary>
    /// Targetable defensive construct summoned by the Shield spell. Lives in
    /// <see cref="HeroView.ShieldSlot"/> and acts as the highest-priority target for melee
    /// attackers and most nukes. Grants no XP when hit or killed.
    /// </summary>
    public class Shield : Targetable
    {
        [field: SerializeField] public ShieldSO Data { get; private set; }

        public int Level { get; private set; } = 1;
        public ShieldAnimator ShieldAnimator { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            ShieldAnimator = GetComponentInChildren<ShieldAnimator>();
        }

        /// <summary>
        /// Configure HP for the given level and play the summon animation. Must be called
        /// once right after instantiation (see <c>ShieldResolver</c>).
        /// </summary>
        public void Init(int level)
        {
            Level = level;
            Health.Init(RewardBonuses.ApplyBonuses(Data, Data.GetHpForLevel(level)));
        }

        /// <summary>
        /// Increase shield level and fully refill HP. Called when the player rolls a higher
        /// shield level than the one currently on field.
        /// </summary>
        public void Promote(int level)
        {
            Level = level;
            float maxHp = RewardBonuses.ApplyBonuses(Data, Data.GetHpForLevel(level));
            Health.Init(maxHp);
            ShieldAnimator.PlayPromote();
        }

        public override void OnSummon()
        {
            base.OnSummon();
            ShieldAnimator.PlaySummon();
        }

        protected override void HandleDeath()
        {
            base.HandleDeath();
            // Free the slot immediately so a new shield can be rolled next turn, then play
            // the scale-out tween. We don't pool — once the tween finishes the GO is destroyed.
            Slot.Unit = null;
            ShieldAnimator.PlayDeath(() => Destroy(gameObject));
        }

        public override void DestroyUnit()
        {
            // Shields skip the standard cleanup path: death tween already destroys the GO.
        }
    }
}

