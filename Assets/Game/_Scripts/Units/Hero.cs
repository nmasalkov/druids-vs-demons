using System;
using _Scripts.Creatures;
using Game._Scripts.Global;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    [RequireComponent(typeof(HeroAnimator))]
    public class Hero : Unit
    {
        [field: SerializeField] public HeroSO Data { get; private set; }
        [field: SerializeField] public Transform CastOrigin { get; private set; }

        public static event Action<Hero> OnHeroDied;

        protected override void Start()
        {
            InitHealth();
            base.Start();
            Health.onDeath += () => OnHeroDied?.Invoke(this);
            GameManager.OnBattleRestart += InitHealth;
        }

        private void OnDestroy()
        {
            GameManager.OnBattleRestart -= InitHealth;
        }

        protected override float GetMaxHealth()
        {
            return Data.health;
        }
    }
}
