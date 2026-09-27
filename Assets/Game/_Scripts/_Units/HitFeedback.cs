using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game._Scripts.Creatures
{
    /// <summary>
    /// Child helper on every <see cref="Targetable"/> (units, hero avatars, shields), always an
    /// instance of <c>_Prefabs/Feedbacks/HitFeedback.prefab</c>. Exposes the world anchor where
    /// projectiles and nuke effects should land — each unit type tunes its own visual hit point
    /// (chest / center / head) via an override on that anchor — and plays the target-side
    /// feedbacks for a creature attack or damaging nuke landing on it. See docs/Battle.md.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [field: SerializeField] public Transform HitPlacePosition { get; private set; }

        [Tooltip("Played for every hit that lands on this target, crit or not (the MMF_Player on this " +
                 "root). Its 'Hit Events' Unity Events feedback is empty by default; a prefab that wants " +
                 "a reaction overrides it — e.g. FinalBoss wires it to BossAnimator.PlayHurt. See " +
                 "docs/Battle.md 'Hit feedbacks'.")]
        [SerializeField] private MMF_Player hitFeedback;

        [Tooltip("Played additionally when a critical strike lands on this target (the CritFeedback " +
                 "child). A small camera shake for now — MMF_CameraShake, picked up by the " +
                 "MMCameraShaker on BattleScene's CameraRig/CameraShaker.")]
        [SerializeField] private MMF_Player critFeedback;

        /// <summary>
        /// Called the moment damage lands on this target — by AttacksResolver for creature attacks and
        /// by the damaging nuke animations (Starfall, FireMagic) on impact. Animated path only: the
        /// instant paths play no feedbacks. Always plays <see cref="hitFeedback"/>; a critical hit
        /// also plays <see cref="critFeedback"/>.
        /// </summary>
        public void PlayHitFeedbacks(bool isCritical)
        {
            hitFeedback.PlayFeedbacks();
            if (!isCritical) return;
            critFeedback.PlayFeedbacks();
        }
    }
}
