using System;
using System.Collections.Generic;
using _Scripts.Creatures;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Pure view for the Starfall nuke. Each shot spawns a <see cref="SimpleProjectile"/>
    /// (Star) above its target's hit anchor and lets it fall straight down. The hero is NOT
    /// the caster of the projectiles — origin is computed per-target. Applies damage on
    /// impact via <see cref="NukeShot.Apply"/>.
    /// </summary>
    public class StarfallAnimation : NukeActionAnimation
    {
        [Header("Cast Timing")]
        [Tooltip("Delay between starting the cast and firing all stars (fired simultaneously).")]
        [SerializeField] private float castDelay = 0.2f;

        [Tooltip("Extra time held after the last impact before completing.")]
        [SerializeField] private float trailingDelay = 0.4f;

        [Header("Projectile")]
        [Tooltip("Self-contained projectile prefab (Star).")]
        [SerializeField] private SimpleProjectile projectilePrefab;

        [Tooltip("How high above the target's hit anchor the star spawns. Should be high " +
                 "enough that the spawn point is off-screen.")]
        [SerializeField] private float spawnHeightAboveTarget = 6f;

        [SerializeField] private float projectileSpeed = 12f;

        public override void Execute(NukeSO source, Hero caster, NukeResolver resolver, Action onComplete)
        {
            var shots = resolver.Shots;
            if (shots.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            ScheduleShotsAndComplete(shots, onComplete);
        }

        private void ScheduleShotsAndComplete(List<NukeShot> shots, Action onComplete)
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
                Utils.DoAfterDelay.Execute(() => FireShot(shot, onShotResolved), castDelay);
            }
        }

        private void FireShot(NukeShot shot, Action onResolved)
        {
            if (shot.Target == null)
            {
                onResolved?.Invoke();
                return;
            }

            Vector3 targetPos = shot.Target.HitFeedback.HitPlacePosition.position;
            Vector3 origin = targetPos + Vector3.up * spawnHeightAboveTarget;
            Quaternion rotation = Quaternion.LookRotation(Vector3.down);

            var projectile = Instantiate(projectilePrefab, origin, rotation);
            projectile.Launch(targetPos, projectileSpeed, () =>
            {
                shot.Apply();
                onResolved?.Invoke();
            });
        }
    }
}



