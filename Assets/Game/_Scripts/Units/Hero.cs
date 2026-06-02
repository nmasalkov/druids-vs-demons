using System;
using _Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    [RequireComponent(typeof(HeroAnimator))]
    public class Hero : Unit
    {
        [field: SerializeField] public HeroSO Data { get; private set; }

        public static event Action<Hero> OnHeroDied;

        protected override void Start()
        {
            InitHealth();
            base.Start();
            Health.onDeath += () => OnHeroDied?.Invoke(this);
        }

        protected override float GetMaxHealth()
        {
            return Data.health;
        }
    }
}
