using UnityEngine;

namespace _Scripts.Creatures
{
    /// <summary>
    /// Cancels the inherited 3D aim rotation on a sprite-based projectile visual so its quad stays
    /// flat to the camera.
    ///
    /// A projectile is aimed with Quaternion.LookRotation (ProjectileAnimatorBase.FireProjectile at
    /// spawn, SimpleProjectile.Update every frame), which points local +Z along travel. That is
    /// correct — load-bearing, even — for the particle-based projectiles: their cone shapes,
    /// local-space velocity/force modules and local simulation space are all authored the 3D way,
    /// with +Z as "forward", so the yaw is what makes their trails spray backward along flight.
    /// A SpriteRenderer's quad, however, lives in its local XY plane, so that same yaw lands it in
    /// the world YZ plane: edge-on to the orthographic camera, zero projected width, invisible.
    ///
    /// Correcting here instead of in SimpleProjectile keeps every particle projectile untouched —
    /// only prefabs that carry this component opt in. Reach for it on any future sprite-based
    /// projectile visual.
    ///
    /// Runs in LateUpdate rather than correcting once because SimpleProjectile.Update rewrites the
    /// parent's rotation every frame for Direct trajectories, so there is no single settle point to
    /// hook (unlike MirrorCorrector's OnRunToSlotArrived). It costs one quaternion write per live
    /// projectile, and projectiles are few and short-lived.
    /// </summary>
    public class BillboardCorrector : MonoBehaviour
    {
        [Tooltip("When checked, the sprite rolls about Z so its local +X points along the " +
                 "projectile's travel direction — for art with an inherent facing (an arrow, a " +
                 "spear). Leave unchecked for art with none (a rock, a ball), which then always " +
                 "renders upright. Either way the quad stays flat to the camera.")]
        [SerializeField] private bool alignToTravelDirection;

        private void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, RollAngle());
        }

        private float RollAngle()
        {
            if (!alignToTravelDirection) return 0f;
            // LookRotation aimed the parent's local +Z along travel, so its forward is that
            // direction in world space; flatten it to a 2D angle.
            Vector3 travel = transform.parent.forward;
            return Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg;
        }
    }
}
