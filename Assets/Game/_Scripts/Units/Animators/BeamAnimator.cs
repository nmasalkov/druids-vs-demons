using UnityEngine;

namespace _Scripts.Creatures
{
    public class BeamAnimator : ProjectileAnimatorBase, ISpecialMissileAnimation
    {
        [Header("Beam Settings")]
        [SerializeField] private float fireRate = 0.2f;

        private Vector3 targetPosition;
        private bool isFiring;
        private float nextFireTime;
        private System.Action onBarrageComplete;

        public void PlayMissileAnimation(Vector3 targetPosition, System.Action onBarrageComplete = null)
        {
            this.targetPosition = targetPosition;
            this.onBarrageComplete = onBarrageComplete;
            isFiring = true;
            nextFireTime = 0f;
        }

        public void StopMissileAnimation()
        {
            isFiring = false;
            onBarrageComplete?.Invoke();
            onBarrageComplete = null;
        }

        private void Update()
        {
            if (!isFiring) return;

            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireRate;
                FireProjectile(targetPosition);
            }
        }
    }
}
