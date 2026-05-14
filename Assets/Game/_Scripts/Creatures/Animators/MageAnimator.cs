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
            StartFreezeSequence(null);
        }

        public override void AttackCreature(Creature target)
        {
            // Call base PlayAttack directly to avoid triggering PlayAttack override
            base.PlayAttack();
            StartFreezeSequence(target);
        }

        public override void AttackWithHits(List<HitInfo> hits)
        {
            if (hits.Count == 0) return;
            pendingOnHit = hits[0].OnHit;
            AttackCreature(hits[0].Target);
        }

        private void StartFreezeSequence(Creature target)
        {
            Utils.DoAfterDelay.Execute(() =>
            {
                skeletonAnimation.timeScale = 0f;

                if (target != null && beamAnimator != null)
                {
                    beamAnimator.PlayMissileAnimation(target.transform, () =>
                    {
                        pendingOnHit?.Invoke();
                        pendingOnHit = null;
                    });
                }
                else
                {
                    Debug.LogWarning($"[MageAnimator] Cannot fire missile. Target: {target}, BeamAnimator: {beamAnimator}", this);
                }

                Utils.DoAfterDelay.Execute(() =>
                {
                    skeletonAnimation.timeScale = 1f;

                    if (beamAnimator != null)
                    {
                        beamAnimator.StopMissileAnimation();
                    }
                }, freezeDuration);
            }, freezeAfter);
        }

        public override float GetAttackDuration()
        {
            return base.GetAttackDuration() + freezeDuration;
        }
    }
}
