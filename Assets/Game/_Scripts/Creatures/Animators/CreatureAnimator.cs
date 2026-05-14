using System;
using System.Collections.Generic;
using Game._Scripts.Creatures;
using Spine.Unity;
using UnityEngine;

namespace _Scripts.Creatures
{
    public class CreatureAnimator : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] protected Transform creatureVisual;

        [Header("Spine")]
        [SerializeField] protected SkeletonAnimation skeletonAnimation;

        [Header("Animations")]
        [SpineAnimation(dataField: "skeletonAnimation")]
        [SerializeField] private string idle = "Idle";

        [SpineAnimation(dataField: "skeletonAnimation")]
        [SerializeField] private string dead = "Dead";
        
        [SpineAnimation(dataField: "skeletonAnimation")]
        [SerializeField] private string walk = "Walk";
        
        [SpineAnimation(dataField: "skeletonAnimation")]
        [SerializeField] private string attack = "Attack";

        public Spine.AnimationState spineAnimationState;

        public string CurrentAnimation;

        protected bool suppressAutoIdle;
        protected bool isDead;

        public bool IsDead => isDead;
        public Transform CreatureVisual => creatureVisual;

        private void Start()
        {
            if (creatureVisual == null)
                creatureVisual = transform;

            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
            }
            
            spineAnimationState = skeletonAnimation.AnimationState;
            spineAnimationState.Complete += HandleAnimationComplete;
            PlayIdle();
        }

        private void OnDestroy()
        {
            if (spineAnimationState != null)
                spineAnimationState.Complete -= HandleAnimationComplete;
        }

        private void HandleAnimationComplete(Spine.TrackEntry trackEntry)
        {
            if (isDead) return;
            if (!trackEntry.Loop && !suppressAutoIdle)
                PlayIdle();
        }

        public void PlayIdle()
        {
            if (isDead) return;
            CurrentAnimation = idle;
            SetAnimation(idle, true);
        }

        public void PlayDead()
        {
            isDead = true;
            suppressAutoIdle = true;
            CurrentAnimation = dead;
            SetAnimation(dead, false);
        }

        public void PlayWalk()
        {
            CurrentAnimation = walk;
            SetAnimation(walk, true);
        }

        public virtual void PlayAttack()
        {
            CurrentAnimation = attack;
            SetAnimation(attack, false);
        }

        public virtual void AttackCreature(Creature target)
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

        public virtual float GetAttackDuration()
        {
            return GetAnimationDuration(attack);
        }

        public float GetAnimationDuration(string animationName)
        {
            if (skeletonAnimation == null || string.IsNullOrEmpty(animationName)) return 0f;
            var anim = skeletonAnimation.Skeleton.Data.FindAnimation(animationName);
            return anim?.Duration ?? 0f;
        }

        private void SetAnimation(string animationName, bool loop, int trackIndex = 0)
        {
            if (string.IsNullOrEmpty(animationName)) return;
            if (spineAnimationState == null) return;
            spineAnimationState.SetAnimation(trackIndex, animationName, loop);
        }
    }
}
