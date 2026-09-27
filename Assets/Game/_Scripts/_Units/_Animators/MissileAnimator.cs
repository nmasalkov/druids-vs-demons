using UnityEngine;

namespace _Scripts.Creatures
{
    public class MissileAnimator : ProjectileAnimatorBase
    {
        public void FireOnce(Vector3 targetPosition, System.Action onHit = null)
        {
            FireProjectile(targetPosition, onHit);
        }
    }
}
