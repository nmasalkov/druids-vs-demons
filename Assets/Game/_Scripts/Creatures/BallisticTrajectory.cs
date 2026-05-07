using DG.Tweening;
using UnityEngine;

namespace _Scripts.Creatures
{
    public static class BallisticTrajectory
    {
        public static Tween Apply(Transform projectile, Vector3 targetPos, float duration, float arcHeight)
        {
            Vector3 startPos = projectile.position;
            Vector3 midPoint = (startPos + targetPos) / 2f + Vector3.up * arcHeight;
            float t = 0f;
            Vector3 previousPos = startPos;

            return DOTween.To(() => t, value =>
            {
                t = value;
                // Quadratic bezier: B(t) = (1-t)²·P0 + 2(1-t)t·P1 + t²·P2
                float oneMinusT = 1f - t;
                Vector3 pos = oneMinusT * oneMinusT * startPos
                              + 2f * oneMinusT * t * midPoint
                              + t * t * targetPos;
                projectile.position = pos;

                // Rotate towards movement direction
                Vector3 dir = pos - previousPos;
                if (dir.sqrMagnitude > 0.0001f)
                    projectile.rotation = Quaternion.LookRotation(dir.normalized);
                previousPos = pos;
            }, 1f, duration).SetEase(Ease.Linear);
        }
    }
}


