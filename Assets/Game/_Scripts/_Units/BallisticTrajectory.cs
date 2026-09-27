using DG.Tweening;
using UnityEngine;

namespace _Scripts.Creatures
{
    public static class BallisticTrajectory
    {
        /// <summary>
        /// Moves the projectile along a quadratic bezier arc. Position only — orientation (aim along
        /// the arc plus optional spin) is owned by <see cref="SimpleProjectile"/>, which hooks the
        /// returned tween's OnUpdate.
        /// </summary>
        public static Tween Apply(Transform projectile, Vector3 targetPos, float duration, float arcHeight)
        {
            Vector3 startPos = projectile.position;
            Vector3 midPoint = (startPos + targetPos) / 2f + Vector3.up * arcHeight;
            float t = 0f;

            return DOTween.To(() => t, value =>
            {
                t = value;
                // Quadratic bezier: B(t) = (1-t)²·P0 + 2(1-t)t·P1 + t²·P2
                float oneMinusT = 1f - t;
                projectile.position = oneMinusT * oneMinusT * startPos
                                      + 2f * oneMinusT * t * midPoint
                                      + t * t * targetPos;
            }, 1f, duration).SetEase(Ease.Linear);
        }
    }
}
