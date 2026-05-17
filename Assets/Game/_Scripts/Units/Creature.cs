using _Scripts.Creatures;
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
            return stats.health;
        }

        public void DestroyCreature() => DestroyUnit();
    }
}