using System;
using System.Collections.Generic;
using DG.Tweening;
using Game._Scripts.Creatures;
using UnityEngine;

namespace _Scripts.Creatures
{
    public class TankAnimator : CreatureAnimator
    {
        [Header("Tank Attack")]
        [SerializeField] private float rangedDelay = 2.5f;
        [SerializeField] private float runDuration = 0.5f;
        [SerializeField] private float returnDuration = 0.3f;
        [SerializeField] private float returnJumpPower = 1.5f;

        public event Action OnAttackAnimStarted;
        public event Action OnLeapBackStarted;
        public event Action OnTankAttackFinished;

        public bool IsWaiting { get; private set; }

        private Transform targetMeleePosition;
        private Vector3 originalPosition;
        private Targetable pendingTarget;
        private Action pendingOnHit;
        private Creature ownerCreature;

        private void Awake()
        {
            ownerCreature = GetComponent<Creature>();
        }

        public override void AttackCreature(Targetable target)
        {
            AttackCreatureInternal(target, rangedDelay);
        }

        public override void AttackWithHits(List<HitInfo> hits)
        {
            if (hits.Count == 0) return;
            pendingOnHit = hits[0].OnHit;
            AttackCreature(hits[0].Target);
        }

        private void AttackCreatureInternal(Targetable target, float delay)
        {
            IsWaiting = false;
            suppressAutoIdle = true;
            ownerCreature.Health.PostponeDeath = true;

            targetMeleePosition = target.Slot.MeleeAttackerPosition;
            originalPosition = creatureVisual.position;

            if (delay > 0f)
                Utils.DoAfterDelay.Execute(RunToTarget, delay);
            else
                RunToTarget();
        }

        public void WaitThenAttack(Targetable target, Action onHit = null)
        {
            IsWaiting = true;
            pendingTarget = target;
            pendingOnHit = onHit;
            suppressAutoIdle = true;
        }

        public void StartPendingAttack()
        {
            if (!IsWaiting || pendingTarget == null) return;
            IsWaiting = false;
            var target = pendingTarget;
            pendingTarget = null;
            AttackCreatureInternal(target, 0f);
        }

        public void SetPendingOnHit(Action onHit)
        {
            pendingOnHit = onHit;
        }

        public void PlayAttackInPlace(Action onFinished)
        {
            suppressAutoIdle = true;
            ownerCreature.Health.PostponeDeath = true;
            PlayAttackAndApplyDamage(() => OnAttackInPlaceFinished(onFinished));
        }

        private void OnAttackInPlaceFinished(Action onFinished)
        {
            suppressAutoIdle = false;
            onFinished?.Invoke();
            FinishSequence();
        }

        private void RunToTarget()
        {
            float walkAnimDuration = GetAnimationDuration("Walk");
            float neededTimeScale = walkAnimDuration > 0 ? walkAnimDuration / runDuration : 1f;
            skeletonAnimation.timeScale = neededTimeScale;
            PlayWalk();

            creatureVisual.DOMove(targetMeleePosition.position, runDuration)
                .SetEase(Ease.Linear)
                .OnComplete(OnArrivedAtTarget);
        }

        private void OnArrivedAtTarget()
        {
            skeletonAnimation.timeScale = 1f;
            OnAttackAnimStarted?.Invoke();
            PlayAttackAndApplyDamage(ReturnToOrigin);
        }

        /// <summary>
        /// Plays attack animation and fires pendingOnHit at mid-point. Calls onComplete when animation ends.
        /// </summary>
        private void PlayAttackAndApplyDamage(Action onComplete)
        {
            float attackDur = base.GetAttackDuration();
            base.PlayAttack();

            Utils.DoAfterDelay.Execute(() =>
            {
                pendingOnHit?.Invoke();
                pendingOnHit = null;
            }, attackDur * 0.5f);

            Utils.DoAfterDelay.Execute(onComplete, attackDur);
        }

        private void ReturnToOrigin()
        {
            PlayIdle();
            OnLeapBackStarted?.Invoke();

            creatureVisual.DOJump(originalPosition, returnJumpPower, 1, returnDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    suppressAutoIdle = false;
                    OnTankAttackFinished?.Invoke();
                    FinishSequence();
                });
        }

        /// <summary>
        /// Called at the end of any attack path. Executes postponed death or returns to idle.
        /// </summary>
        private void FinishSequence()
        {
            if (ownerCreature.Health.IsDead())
            {
                ownerCreature.Health.ExecutePostponedDeath();
            }
            else
            {
                ownerCreature.Health.PostponeDeath = false;
                PlayIdle();
            }
        }

        public override float GetAttackDuration()
        {
            return rangedDelay + runDuration + base.GetAttackDuration() + returnDuration;
        }

        public float GetRangedDelay() => rangedDelay;
        public float GetRunDuration() => runDuration;
    }
}
