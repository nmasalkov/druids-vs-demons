using UnityEngine;

namespace _Scripts.Creatures
{
    public class MageAnimator : CreatureAnimator
    {
        [Header("Mage Attack")]
        [SerializeField] private float freezeAfter = 0.3f;
        [SerializeField] private float freezeDuration = 1.5f;

        public override void PlayAttack()
        {
            base.PlayAttack();

            Utils.DoAfterDelay.Execute(() =>
            {
                skeletonAnimation.timeScale = 0f;

                Utils.DoAfterDelay.Execute(() =>
                {
                    skeletonAnimation.timeScale = 1f;
                }, freezeDuration);
            }, freezeAfter);
        }

        public override float GetAttackDuration()
        {
            return base.GetAttackDuration() + freezeDuration;
        }
    }
}

