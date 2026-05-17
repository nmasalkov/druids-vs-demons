using UnityEngine;

namespace _Scripts.Creatures
{
    public class MissileAnimator : ProjectileAnimatorBase
    {
        public void FireOnce(Transform target, System.Action onHit = null)
        {
            FireProjectile(target, onHit);
        }
    }
}

