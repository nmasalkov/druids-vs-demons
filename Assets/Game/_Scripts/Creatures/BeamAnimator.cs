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

        public void PlayMissileAnimation(Transform target)
        {
            this.target = target;
            isFiring = true;
            nextFireTime = 0f;
        }

        public void StopMissileAnimation()
        {
            isFiring = false;
            this.target = null;
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









