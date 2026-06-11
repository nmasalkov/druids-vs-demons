using Game._Scripts.Creatures;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Shock shot — applies the Shocked status to the target. Heroes are immune (the projectile
    /// still flies but does nothing on impact), so a Shock cast with no creature targets is
    /// effectively wasted on the Hero.
    /// </summary>
    public class ShockShot : NukeShot
    {
        public override void Apply()
        {
            if (Target is Hero) return;
            ApplyShock();
        }
    }
}

