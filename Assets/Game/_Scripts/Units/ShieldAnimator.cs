using System;
using DG.Tweening;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    /// <summary>
    /// View-layer helper for <see cref="Shield"/>. Owns the scale-in/scale-out tweens and the
    /// visual root toggle. Per-skin timings live directly on this component (each shield prefab
    /// can tune them independently).
    /// </summary>
    public class ShieldAnimator : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;

        [Header("Summon (grow-in)")]
        [SerializeField] private float summonDuration = 0.4f;
        [SerializeField] private float summonStartScale = 0.1f;
        [SerializeField] private Ease summonEase = Ease.OutBack;

        [Header("Promote (re-cast at higher level)")]
        [SerializeField] private float promoteDuration = 0.25f;
        [SerializeField] private float promotePunchScale = 1.25f;
        [SerializeField] private Ease promoteEase = Ease.OutBack;

        [Header("Death (vanish)")]
        [SerializeField] private float deathDuration = 0.35f;
        [SerializeField] private float deathEndScale = 0.01f;
        [SerializeField] private Ease deathEase = Ease.InBack;

        private Vector3 baseScale;
        private Tween activeTween;

        void Awake()
        {
            baseScale = visualRoot.localScale;
        }

        public void PlaySummon()
        {
            KillActive();
            visualRoot.gameObject.SetActive(true);
            visualRoot.localScale = baseScale * summonStartScale;
            activeTween = visualRoot
                .DOScale(baseScale, summonDuration)
                .SetEase(summonEase);
        }

        public void PlayPromote()
        {
            KillActive();
            visualRoot.localScale = baseScale * promotePunchScale;
            activeTween = visualRoot
                .DOScale(baseScale, promoteDuration)
                .SetEase(promoteEase);
        }

        public void PlayDeath(Action onComplete)
        {
            KillActive();
            activeTween = visualRoot
                .DOScale(baseScale * deathEndScale, deathDuration)
                .SetEase(deathEase)
                .OnComplete(() =>
                {
                    visualRoot.gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
        }

        private void KillActive()
        {
            if (activeTween != null && activeTween.IsActive())
                activeTween.Kill();
        }

        void OnDestroy()
        {
            KillActive();
        }
    }
}

