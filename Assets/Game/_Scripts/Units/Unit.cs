using _Scripts.Creatures;
using Game._Scripts.Units;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    [RequireComponent(typeof(StatusesManager))]
    public abstract class Unit : Targetable
    {
        public UnitAnimator Animator { get; private set; }
        public StatusesManager StatusesManager { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Animator = GetComponent<UnitAnimator>();
            StatusesManager = GetComponent<StatusesManager>();
        }

        protected abstract float GetMaxHealth();

        public void InitHealth()
        {
            Health.Init(GetMaxHealth());
        }
    }
}



