using System;
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

        public override void AttackCreature(Targetable target)
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
                OnVolleyFinished();
                return;
            }

            shotsRemaining--;
            skeletonAnimation.timeScale = currentTimeScale;

            base.PlayAttack();
            var entry = spineAnimationState.GetCurrent(0);

            var hit = pendingHits[currentHitIndex];
            Action extraShot = BuildExtraShot(currentHitIndex);
            currentHitIndex++;

            Action mainShot = BuildShot(missileAnimator, hit);
            Utils.DoAfterDelay.Execute(() =>
            {
                mainShot?.Invoke();
                extraShot?.Invoke();
            }, scaledFireDelay);

            if (entry != null)
            {
                entry.Complete += OnShotAnimComplete;
            }
        }

        /// <summary>
        /// Fires <paramref name="hit"/> from <paramref name="launcher"/>, aiming at where the target stands
        /// now (captured immediately, so a target that dies before the fire moment doesn't matter).
        /// Null for a hit with no target — that shot plays its animation but launches nothing.
        /// </summary>
        protected static Action BuildShot(MissileAnimator launcher, HitInfo hit)
        {
            if (hit.Target == null) return null;
            Vector3 targetPos = hit.Target.HitFeedback.HitPlacePosition.position;
            var onHit = hit.OnHit;
            return () => launcher.FireOnce(targetPos, onHit);
        }

        /// <summary>Extra missiles fired at the same moment as shot <paramref name="shotIndex"/>'s
        /// main missile; null for a plain archer. See SplitShotArcherAnimator.</summary>
        protected virtual Action BuildExtraShot(int shotIndex) => null;

        /// <summary>Called once the volley's last shot animation has finished and the archer is back
        /// to idle.</summary>
        protected virtual void OnVolleyFinished() { }

        private void OnShotAnimComplete(Spine.TrackEntry trackEntry)
        {
            trackEntry.Complete -= OnShotAnimComplete;
            PlayNextShot();
        }

        public override void PlayAttack()
        {
            base.PlayAttack();
        }

        public override float GetAttackDuration() => GetAttackDuration(shotCount);

        /// <summary>Duration of a volley of <paramref name="shots"/> shots (capped at totalDuration,
        /// matching the time-scaling in AttackWithHits).</summary>
        public float GetAttackDuration(int shots)
        {
            float singleAnimDuration = base.GetAttackDuration();
            float naturalTotal = singleAnimDuration * shots;
            return naturalTotal > totalDuration ? totalDuration : naturalTotal;
        }
    }
}
