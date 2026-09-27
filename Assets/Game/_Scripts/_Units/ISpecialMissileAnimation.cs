using UnityEngine;

namespace _Scripts.Creatures
{
    public interface ISpecialMissileAnimation
    {
        void PlayMissileAnimation(Vector3 targetPosition, System.Action onBarrageComplete = null);
        void StopMissileAnimation();
    }
}
