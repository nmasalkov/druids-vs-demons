namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Starfall shot — deals a flat amount of damage to <see cref="NukeShot.Target"/>.
    /// Behaviour-identical to <see cref="FireMagicShot"/>; kept as a parallel type so each
    /// nuke owns its own shot class and future tweaks (crit chance, on-hit FX, etc.) can
    /// diverge without touching FireMagic.
    /// </summary>
    public class StarfallShot : NukeShot
    {
        public float Damage;

        /// <summary>An improved-Starfall bonus strike (see <see cref="StarfallResolver"/>) — the
        /// animation fires it after the main volley instead of with it. No gameplay difference.</summary>
        public bool IsExtraStrike;

        public override void Apply() => ApplyDamage(Damage);
    }
}
