using System;
using System.Collections.Generic;
using DG.Tweening;
using Game._Scripts.Creatures;
using Spine.Unity;
using UnityEngine;

namespace _Scripts.Creatures
{
    public class CreatureAnimator : UnitAnimator
    {
        [Header("Creature Animations")]
        [SpineAnimation(dataField: "skeletonAnimation")]
        [SerializeField] private string walk = "Walk";

        [Header("Run To Slot")]
        [Tooltip("World units per second when running into a new slot (e.g. after being charmed).")]
        [SerializeField] private float runSpeed = 7f;
        [Tooltip("Lower bound so very short hops still read as a run.")]
        [SerializeField] private float minRunDuration = 0.35f;

        /// <summary>Fired when a <see cref="RunToCurrentSlot"/> move finishes and the creature
        /// has snapped into its slot.</summary>
        public event Action OnRunToSlotArrived;

        public void PlayWalk()
        {
            CurrentAnimation = walk;
            SetAnimation(walk, true);
        }

        /// <summary>
        /// Scales the Walk animation's playback speed so one cycle takes exactly
        /// <paramref name="runDuration"/> seconds, then starts it looping. Mirrors the timescale
        /// trick <c>TankAnimator</c> uses for its melee run-in. Caller is responsible for calling
        /// <see cref="EndScaledWalk"/> once the accompanying move finishes.
        /// </summary>
        public void BeginScaledWalk(float runDuration)
        {
            float walkAnimDuration = GetAnimationDuration(walk);
            skeletonAnimation.timeScale = walkAnimDuration > 0 && runDuration > 0
                ? walkAnimDuration / runDuration
                : 1f;
            PlayWalk();
        }

        public void EndScaledWalk()
        {
            skeletonAnimation.timeScale = 1f;
            PlayIdle();
        }

        /// <summary>
        /// Runs the creature's root from <paramref name="fromWorldPosition"/> into its current
        /// slot (the root's parent). During the run the root is counter-flipped so the sprite —
        /// and its HP bar — keep the facing they had before the reparent onto a mirrored side;
        /// on arrival it snaps to the slot's own orientation (the "turn around"). Moves the root,
        /// not creatureVisual, so the HP bar and status feedbacks carry along. Fires
        /// <see cref="OnRunToSlotArrived"/> when settled.
        /// </summary>
        public void RunToCurrentSlot(Vector3 fromWorldPosition)
        {
            var root = transform;
            var destination = root.position;

            root.position = fromWorldPosition;
            root.localScale = new Vector3(-1f, 1f, 1f);

            float distance = Vector3.Distance(fromWorldPosition, destination);
            float runDuration = Mathf.Max(distance / runSpeed, minRunDuration);
            BeginScaledWalk(runDuration);

            root.DOMove(destination, runDuration)
                .SetEase(Ease.Linear)
                .OnComplete(FinishRunToSlot);
        }

        private void FinishRunToSlot()
        {
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            EndScaledWalk();
            OnRunToSlotArrived?.Invoke();
        }

        public virtual void AttackCreature(Targetable target)
        {
            PlayAttack();
        }

        /// <summary>
        /// Attack with per-hit damage callbacks. Each HitInfo contains a target and an onHit action.
        /// </summary>
        public virtual void AttackWithHits(List<HitInfo> hits)
        {
            if (hits.Count > 0)
                AttackCreature(hits[0].Target);
        }
    }
}
