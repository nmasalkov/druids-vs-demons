using UnityEngine;

namespace _Scripts.Creatures
{
    public interface ISpecialMissileAnimation
    {
        void PlayMissileAnimation(Transform target, System.Action onBarrageComplete = null);
        void StopMissileAnimation();
    }
}
