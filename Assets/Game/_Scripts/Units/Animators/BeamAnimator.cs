using UnityEngine;

namespace _Scripts.Creatures
{
    public class BeamAnimator : ProjectileAnimatorBase, ISpecialMissileAnimation
    {
        [Header("Beam Settings")]
        [SerializeField] private float fireRate = 0.2f;

        private Transform target;
        private bool isFiring;
        private float nextFireTime;
        private System.Action onBarrageComplete;

        public void PlayMissileAnimation(Transform target, System.Action onBarrageComplete = null)
        {
            this.target = target;
            this.onBarrageComplete = onBarrageComplete;
            isFiring = true;
            nextFireTime = 0f;
        }

        public void StopMissileAnimation()
        {
            isFiring = false;
            this.target = null;
            onBarrageComplete?.Invoke();
            onBarrageComplete = null;
        }

        private void Update()
        {
            if (!isFiring || target == null) return;

            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireRate;
                FireProjectile(target);
            }
        }
    }
}









