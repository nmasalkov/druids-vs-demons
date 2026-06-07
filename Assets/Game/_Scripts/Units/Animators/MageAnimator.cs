using System;
using System.Collections.Generic;
using Game._Scripts.Creatures;
using UnityEngine;

namespace _Scripts.Creatures
{
    public class MageAnimator : CreatureAnimator
    {
        [Header("Mage Attack")]
        [SerializeField] private float freezeAfter = 0.3f;
        [SerializeField] private float freezeDuration = 1.5f;

        [Header("Beam")]
        [SerializeField] private BeamAnimator beamAnimator;

        private Action pendingOnHit;

        public override void PlayAttack()
        {
            base.PlayAttack();
            StartFreezeSequence(fireBeam: false, Vector3.zero);
        }

        public override void AttackCreature(Unit target)
        {
            // Call base PlayAttack directly to avoid triggering PlayAttack override
            base.PlayAttack();
            StartFreezeSequence(fireBeam: true, target.HitFeedback.HitPlacePosition.position);
        }

        public override void AttackWithHits(List<HitInfo> hits)
        {
            if (hits.Count == 0) return;
            pendingOnHit = hits[0].OnHit;
            AttackCreature(hits[0].Target);
        }

        private void StartFreezeSequence(bool fireBeam, Vector3 targetPosition)
        {
            Utils.DoAfterDelay.Execute(() =>
            {
                skeletonAnimation.timeScale = 0f;

                if (fireBeam)
                {
                    beamAnimator.PlayMissileAnimation(targetPosition, () =>
                    {
                        pendingOnHit?.Invoke();
                        pendingOnHit = null;
                    });
                }

                Utils.DoAfterDelay.Execute(() =>
                {
                    skeletonAnimation.timeScale = 1f;
                    beamAnimator.StopMissileAnimation();
                }, freezeDuration);
            }, freezeAfter);
        }

        public override float GetAttackDuration()
        {
            return base.GetAttackDuration() + freezeDuration;
        }
    }
}
