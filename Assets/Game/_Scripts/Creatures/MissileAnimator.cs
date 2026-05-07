using UnityEngine;

namespace _Scripts.Creatures
{
    public class MissileAnimator : ProjectileAnimatorBase
    {
        public void FireOnce(Transform target)
        {
            FireProjectile(target);
        }
    }
}

