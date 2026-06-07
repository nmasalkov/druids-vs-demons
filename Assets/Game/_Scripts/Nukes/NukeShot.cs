using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Pre-computed unit of nuke damage. Produced by a NukeResolver and consumed by a
    /// NukeActionAnimation (which decides when to apply it — e.g. on projectile impact)
    /// or applied immediately via NukeResolver.ApplyInstant.
    /// </summary>
    public struct NukeShot
    {
        public Unit Target;
        public float Damage;

        public void Apply()
        {
            if (Target == null) return;
            Target.Health.TakeDamage(Damage);
        }
    }
}
