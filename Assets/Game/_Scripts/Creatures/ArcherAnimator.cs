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
        private Transform currentTarget;
        private float currentTimeScale;
        private float scaledFireDelay;

        public override void AttackCreature(Creature target)
        {
            float singleAnimDuration = base.GetAttackDuration();
            float naturalTotal = singleAnimDuration * shotCount;

            currentTimeScale = naturalTotal > totalDuration ? naturalTotal / totalDuration : 1f;
            float scaledAnimDuration = singleAnimDuration / currentTimeScale;
            scaledFireDelay = scaledAnimDuration * (fireAtPercent / 100f);

            currentTarget = target != null ? target.transform : null;
            shotsRemaining = shotCount;

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

            // Set the animation and get the track entry for event subscription
            base.PlayAttack();
            var entry = spineAnimationState.GetCurrent(0);

            // Schedule projectile fire at the configured percentage
            if (currentTarget != null)
            {
                Utils.DoAfterDelay.Execute(() =>
                {
                    if (currentTarget != null)
                        missileAnimator.FireOnce(currentTarget);
                }, scaledFireDelay);
            }

            // Listen for THIS specific animation complete to chain next shot
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

