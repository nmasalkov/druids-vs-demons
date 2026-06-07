using System.Collections.Generic;
using Game._Scripts.Creatures;
using UnityEngine;

namespace _Scripts.Creatures
{
    public class ArcherAnimator : CreatureAnimator
    {
        [Header("Archer Attack")]
        [SerializeField] private MissileAnimator missileAnimator;
        [SerializeField] private int shotCount = 2;
        [SerializeField] private float totalDuration = 2.2f;

        [Header("Fire Timing")]
        [Range(0f, 100f)]
        [SerializeField] private float fireAtPercent = 50f;

        private int shotsRemaining;
        private float currentTimeScale;
        private float scaledFireDelay;
        private List<HitInfo> pendingHits;
        private int currentHitIndex;

        public override void AttackCreature(Unit target)
        {
            AttackWithHits(new List<HitInfo>
            {
                new HitInfo { Target = target, OnHit = null }
            });
        }

        public override void AttackWithHits(List<HitInfo> hits)
        {
            pendingHits = hits;
            currentHitIndex = 0;

            float singleAnimDuration = base.GetAttackDuration();
            int actualShotCount = hits.Count;
            float naturalTotal = singleAnimDuration * actualShotCount;

            currentTimeScale = naturalTotal > totalDuration ? naturalTotal / totalDuration : 1f;
            float scaledAnimDuration = singleAnimDuration / currentTimeScale;
            scaledFireDelay = scaledAnimDuration * (fireAtPercent / 100f);

            shotsRemaining = actualShotCount;

            suppressAutoIdle = true;
            PlayNextShot();
        }

        private void PlayNextShot()
        {
            if (shotsRemaining <= 0)
            {
                skeletonAnimation.timeScale = 1f;
                suppressAutoIdle = false;
                PlayIdle();
                return;
            }

            shotsRemaining--;
            skeletonAnimation.timeScale = currentTimeScale;

            base.PlayAttack();
            var entry = spineAnimationState.GetCurrent(0);

            var hit = pendingHits[currentHitIndex];
            currentHitIndex++;

            if (hit.Target != null)
            {
                Vector3 targetPos = hit.Target.HitFeedback.HitPlacePosition.position;
                var onHit = hit.OnHit;
                Utils.DoAfterDelay.Execute(() =>
                {
                    missileAnimator.FireOnce(targetPos, onHit);
                }, scaledFireDelay);
            }

            if (entry != null)
            {
                entry.Complete += OnShotAnimComplete;
            }
        }

        private void OnShotAnimComplete(Spine.TrackEntry trackEntry)
        {
            trackEntry.Complete -= OnShotAnimComplete;
            PlayNextShot();
        }

        public override void PlayAttack()
        {
            base.PlayAttack();
        }

        public override float GetAttackDuration()
        {
            float singleAnimDuration = base.GetAttackDuration();
            float naturalTotal = singleAnimDuration * shotCount;
            return naturalTotal > totalDuration ? totalDuration : naturalTotal;
        }
    }
}
