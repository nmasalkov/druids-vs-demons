using System;
using System.Collections.Generic;
using _Scripts.Creatures;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Pure view for the FireMagic ("laser") nuke. Takes the pre-computed shot list from
    /// <see cref="FireMagicResolver"/> and fires one <see cref="SimpleProjectile"/> per shot,
    /// applying damage on impact. No targeting / damage logic here — that's resolver's job.
    /// </summary>
    public class FireMagicAnimation : NukeActionAnimation
    {
        [Header("Cast Timing")]
        [Tooltip("Delay between starting the cast and firing the first shot.")]
        [SerializeField] private float castDelay = 0.4f;

        [Tooltip("Pause between consecutive shots at different targets.")]
        [SerializeField] private float pauseBetweenShots = 0.5f;

        [Tooltip("Extra time held after the last impact before completing.")]
        [SerializeField] private float trailingDelay = 0.4f;

        [Header("Projectile")]
        [Tooltip("Self-contained projectile prefab (handles muzzle/trail/impact VFX internally).")]
        [SerializeField] private SimpleProjectile projectilePrefab;

        [SerializeField] private float projectileSpeed = 10f;

        public override void Execute(NukeSO source, Hero caster, NukeResolver resolver, Action onComplete)
        {
            var shots = resolver.Shots;
            if (shots.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            ScheduleShotsAndComplete(caster, shots, onComplete);
        }

        private void ScheduleShotsAndComplete(Hero caster, List<NukeShot> shots, Action onComplete)
        {
            int remainingShots = shots.Count;
            Action onShotResolved = () =>
            {
                remainingShots--;
                if (remainingShots > 0) return;

                Utils.DoAfterDelay.Execute(() => onComplete?.Invoke(), trailingDelay);
            };

            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                float shotStartDelay = castDelay + i * pauseBetweenShots;
                Utils.DoAfterDelay.Execute(() => FireShot(caster, shot, onShotResolved), shotStartDelay);
            }
        }

        private void FireShot(Hero caster, NukeShot shot, Action onResolved)
        {
            if (shot.Target == null)
            {
                onResolved?.Invoke();
                return;
            }

            Vector3 targetPos = shot.Target.HitFeedback.HitPlacePosition.position;
            Vector3 origin = caster.CastOrigin.position;
            Vector3 direction = targetPos - origin;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized)
                : Quaternion.identity;

            var projectile = Instantiate(projectilePrefab, origin, rotation);
            projectile.Launch(targetPos, projectileSpeed, () =>
            {
                shot.Apply();
                onResolved?.Invoke();
            });
        }
    }
}
