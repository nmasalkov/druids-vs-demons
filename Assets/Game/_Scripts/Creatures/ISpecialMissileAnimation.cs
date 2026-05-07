using UnityEngine;

namespace _Scripts.Creatures
{
    public interface ISpecialMissileAnimation
    {
        void PlayMissileAnimation(Transform target);
        void StopMissileAnimation();
    }
}
