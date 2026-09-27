namespace Game._Scripts.Pickups
{
    using System;
    using DG.Tweening;
    using UnityEngine;
    using Random = UnityEngine.Random;

    public class ExpirienceGem : MonoBehaviour
    {
        [SerializeField] private float flyDuration = 0.8f;
        [SerializeField] private float curveStrength = 2f;
        [SerializeField] private Ease ease = Ease.InOutQuad;
        [SerializeField] private float dropRadius = 1f;
        [SerializeField] private float dropDuration = 0.5f;
        [SerializeField] private float dropArcHeight = 1.5f;

        /// <summary>
        /// Flies the gem to the target with a curved path. Calls onArrive and destroys itself.
        /// </summary>
        public void FlyTo(Transform destination, Action onArrive)
        {
            Vector3 start = transform.position;
            Vector3 end = destination.position;
            Vector3 direction = end - start;
            Vector3 midPoint = start + direction * 0.3f;

            Vector3 perp = Vector3.Cross(direction.normalized, Vector3.forward);
            float randomOffset = Random.Range(-curveStrength, curveStrength);
            if (Mathf.Abs(randomOffset) < curveStrength * 0.4f)
                randomOffset = curveStrength * 0.4f * Mathf.Sign(randomOffset == 0 ? 1 : randomOffset);

            midPoint += perp * randomOffset;

            Vector3[] path = { start, midPoint, end };

            transform.DOPath(path, flyDuration, PathType.CatmullRom)
                .SetEase(ease)
                .OnComplete(() =>
                {
                    onArrive?.Invoke();
                    Destroy(gameObject);
                });

            transform.DOScale(Vector3.zero, flyDuration * 0.3f)
                .SetDelay(flyDuration * 0.7f)
                .SetEase(Ease.InBack);
        }

        /// <summary>
        /// Drops the gem with a small arc to a random nearby position.
        /// </summary>
        public void Drop(Action onComplete = null)
        {
            Vector3 start = transform.position;
            Vector2 randomDir = Random.insideUnitCircle.normalized * Random.Range(dropRadius * 0.4f, dropRadius);
            Vector3 end = start + new Vector3(randomDir.x, randomDir.y * 0.5f, 0f);

            Vector3 midPoint = (start + end) * 0.5f + Vector3.up * dropArcHeight;

            Vector3[] path = { start, midPoint, end };

            transform.DOPath(path, dropDuration, PathType.CatmullRom)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => onComplete?.Invoke());

            float randomSpin = (Random.value > 0.5f ? 1f : -1f) * 720f;
            transform.DORotate(new Vector3(0f, 0f, randomSpin), dropDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuad);
        }
    }
}