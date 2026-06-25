using System.Collections.Generic;
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

        public void PlayWalk()
        {
            CurrentAnimation = walk;
            SetAnimation(walk, true);
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
