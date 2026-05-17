using _Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    [RequireComponent(typeof(HeroAnimator))]
    public class Hero : Unit
    {
        [field: SerializeField] public HeroSO Data { get; private set; }

        protected override void Start()
        {
            InitHealth();
            base.Start();
        }

        protected override float GetMaxHealth()
        {
            return Data.health;
        }
    }
}

