namespace Game._Scripts.Nukes
{
    /// <summary>
    /// FireMagic shot — deals a flat amount of damage to <see cref="NukeShot.Target"/>.
    /// </summary>
    public class FireMagicShot : NukeShot
    {
        public float Damage;

        public override void Apply() => ApplyDamage(Damage);
    }
}

