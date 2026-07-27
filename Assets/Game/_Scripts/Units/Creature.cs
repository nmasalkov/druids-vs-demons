using _Scripts.Creatures;
using Game._Scripts.Units;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    [RequireComponent(typeof(CreatureAnimator))]
    [RequireComponent(typeof(Experience))]
    public class Creature : Unit
    {
        [field: SerializeField] public CreatureSO Data { get; private set; }

        public new CreatureAnimator Animator { get; private set; }
        public Experience Experience { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Animator = GetComponent<CreatureAnimator>();
            Experience = GetComponent<Experience>();
        }

        protected override void Start()
        {
            InitHealth();
            base.Start();
        }

        protected override float GetMaxHealth()
        {
            var stats = Data.Stats(Experience.Level);
            return RewardBonuses.ApplyBonuses(Data, stats.health);
        }

        /// <summary>
        /// This creature's own side's hero, resolved live off G rather than cached at spawn time —
        /// HeroView.ReplaceHeroAvatar() destroys/replaces the enemy Hero between encounters, so a
        /// cached reference would go stale (rule 22). Only ever checked on death, so the slot scan
        /// is not a per-frame cost.
        /// </summary>
        public Hero OwnerHero => G.PlayerCreaturesManager.GetAllCreatures().Contains(this) ? G.PlayerHero : G.EnemyHero;

        private const float OwnerHeroDamageFraction = 0.2f;

        protected override void HandleDeath()
        {
            base.HandleDeath();
            OwnerHero.Health.TakeDamage(Mathf.Floor(Health.MaxHealth * OwnerHeroDamageFraction));
        }

        public void DestroyCreature() => DestroyUnit();
    }
}