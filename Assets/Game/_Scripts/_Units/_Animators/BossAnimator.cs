using UnityEngine;
using Utils;

namespace _Scripts.Creatures
{
    /// <summary>
    /// Hero avatar animator for visuals driven by a plain Unity <see cref="Animator"/> instead of Spine
    /// (first user: FinalBoss.prefab's Skeleton Warlock rig). Drop-in for <see cref="HeroAnimator"/> —
    /// it IS one, so it satisfies <c>Hero</c>'s <c>[RequireComponent(typeof(HeroAnimator))]</c> and is
    /// what <c>Unit.Animator</c>/<c>Health.animator</c> talk to — but every call is routed through
    /// <see cref="AnimationAPI"/> rather than the inherited SkeletonAnimation, which stays unused.
    /// Adds the boss-only actions (hurt, rise, walk, casts). See docs/AnimationAPI.md.
    /// </summary>
    public class BossAnimator : HeroAnimator
    {
        [Header("Animator")]
        [Tooltip("AnimationAPI on the GameObject holding this avatar's Animator (e.g. the nested " +
                 "'Skeleton Warlock' child). All playback goes through it — its controller must be " +
                 "wired via AnimationAPI's \"Wire controller\" button. See docs/AnimationAPI.md.")]
        [SerializeField] private AnimationAPI animationApi;

        // Names differ from UnitAnimator's private idle/dead/attack: Unity refuses a field name that is
        // serialized twice across a class hierarchy.
        [Header("Animation names (AnimationAPI)")]
        [Tooltip("Resting animation while alive. Enabled (the Animator keeps looping it) on Start and " +
                 "whenever PlayIdle is called.")]
        [SerializeField] private string idleAnimation = "Idle";

        [Tooltip("One-shot played the moment the hero dies (the fall), before settling into Dead Animation.")]
        [SerializeField] private string deathAnimation = "Die";

        [Tooltip("Resting animation after death (lying down). Enabled on death, so it holds once Death " +
                 "Animation finishes — nothing brings it back except PlayRise.")]
        [SerializeField] private string deadAnimation = "Dead";

        [Tooltip("One-shot played by PlayHurt — wired to HitFeedback's Unity Events feedback, so it plays " +
                 "on every creature attack/damaging nuke that lands (docs/Battle.md 'Hit feedbacks'). " +
                 "Ignored once dead.")]
        [SerializeField] private string hurtAnimation = "Hurt";

        [Tooltip("One-shot played by PlayRise (getting back up from Dead); returns to Idle Animation afterwards.")]
        [SerializeField] private string riseAnimation = "Rise";

        [Tooltip("Resting animation enabled by PlayWalk. Call PlayIdle to stop walking.")]
        [SerializeField] private string walkAnimation = "Walk";

        [Tooltip("One-shot played by the generic UnitAnimator.PlayAttack (heroes don't attack today, so " +
                 "this only matters if something starts calling it).")]
        [SerializeField] private string attackAnimation = "Cast01";

        [Tooltip("One-shot played by PlayCast1.")]
        [SerializeField] private string cast1Animation = "Cast01";

        [Tooltip("One-shot played by PlayCast2.")]
        [SerializeField] private string cast2Animation = "Cast02";

        [Tooltip("One-shot played by PlayCast3.")]
        [SerializeField] private string cast3Animation = "Cast03";

        protected override void Start()
        {
            if (creatureVisual == null)
                creatureVisual = transform;
            PlayIdle();
        }

        public override void PlayIdle()
        {
            if (isDead) return;
            animationApi.EnableAnimation(idleAnimation);
        }

        public override void PlayDead()
        {
            isDead = true;
            // Rest in Dead without firing its trigger: Die's trigger is set on this same frame, and two
            // pending triggers would make Dead cut the fall short.
            animationApi.EnableAnimation(deadAnimation, playImmediately: false);
            animationApi.TriggerAnimation(deathAnimation);
        }

        public override void PlayAttack() => PlayOneShot(attackAnimation);

        public override float GetAttackDuration() => GetAnimationDuration(attackAnimation);

        public override float GetAnimationDuration(string animationName) =>
            animationApi.GetClipLength(animationName);

        public void PlayHurt() => PlayOneShot(hurtAnimation);

        public void PlayRise()
        {
            isDead = false;
            animationApi.EnableAnimation(idleAnimation, playImmediately: false);
            animationApi.TriggerAnimation(riseAnimation);
        }

        /// <summary>Length of the Rise clip — lets a caller schedule against the end of PlayRise
        /// without knowing which animation name this boss uses (AnimationAPI has no finished event).
        /// Read by BossPhaseTransitionState, see docs/GameLoop.md.</summary>
        public float GetRiseDuration() => GetAnimationDuration(riseAnimation);

        public void PlayWalk()
        {
            if (isDead) return;
            animationApi.EnableAnimation(walkAnimation);
        }

        public void PlayCast1() => PlayOneShot(cast1Animation);
        public void PlayCast2() => PlayOneShot(cast2Animation);
        public void PlayCast3() => PlayOneShot(cast3Animation);

        /// <summary>One-shots never play on a dead boss — only PlayRise brings it out of Dead.</summary>
        private void PlayOneShot(string animationName)
        {
            if (isDead) return;
            animationApi.TriggerAnimation(animationName);
        }

        public AnimationAPI AnimationApi => animationApi;
    }
}
